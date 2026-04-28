using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Lab5.Shared;

namespace Lab5.Gui;

/// <summary>
/// Główne okno aplikacji: zbiera parametry uruchomienia, odbiera status z workera
/// i rysuje aktualnie najlepszą trasę komiwojażera.
/// </summary>
public partial class MainWindow : Window
{
    private WorkerClient? _client;
    private bool _isPaused;
    private IReadOnlyList<City>? _cities;
    private int[]? _bestRoute;
    private bool _canClose;

    public MainWindow()
    {
        InitializeComponent();
        Closing += MainWindow_Closing;
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_client is not null && _client.IsRunning)
        {
            return;
        }

        try
        {
            var settings = BuildSettings();
            _cities = TspLoader.Load(settings.DataFilePath, settings.CityCount);
            _bestRoute = null;
            TourCanvas.Children.Clear();

            _client = new WorkerClient();
            await _client.StartAsync(
                GetSelectedMode(),
                settings,
                async msg => await Dispatcher.InvokeAsync(() => HandleMessage(msg)),
                CancellationToken.None);

            _isPaused = false;
            PauseResumeButton.Content = "Pauza";
            PauseResumeButton.IsEnabled = true;
            StopButton.IsEnabled = true;
            StartButton.IsEnabled = false;
            CloseButton.IsEnabled = false;
            _canClose = false;
            PhaseValue.Text = "start";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Błąd startu", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void PauseResumeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_client is null)
        {
            return;
        }

        if (_isPaused)
        {
            await _client.ResumeAsync();
            _isPaused = false;
            PauseResumeButton.Content = "Pauza";
        }
        else
        {
            await _client.PauseAsync();
            _isPaused = true;
            PauseResumeButton.Content = "Wznów";
        }
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_client is null)
        {
            return;
        }

        await _client.StopAsync();
        PhaseValue.Text = "stopping";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _canClose = true;
        Close();
    }

    private void HandleMessage(WorkerMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.Phase))
        {
            PhaseValue.Text = message.Phase;
        }

        EpochValue.Text = message.Epoch.ToString();
        ProcessedValue.Text = message.Processed.ToString();

        if (message.BestLength > 0)
        {
            BestValue.Text = message.BestLength.ToString("F2");
        }

        if (message.BestThreadId >= 0)
        {
            ThreadValue.Text = message.BestThreadId.ToString();
        }

        if (message.BestRoutePreview is { Length: > 2 })
        {
            _bestRoute = message.BestRoutePreview;
            DrawRoute();
        }

        if (message.Type.Equals("completed", StringComparison.OrdinalIgnoreCase))
        {
            SetCompletedUi();
        }

        if (message.Type.Equals("error", StringComparison.OrdinalIgnoreCase))
        {
            SetCompletedUi();
            MessageBox.Show(message.Error ?? "Nieznany błąd workera.", "Błąd workera", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetCompletedUi()
    {
        StartButton.IsEnabled = true;
        PauseResumeButton.IsEnabled = false;
        StopButton.IsEnabled = false;
        CloseButton.IsEnabled = true;
        _canClose = true;
    }

    private string GetSelectedMode()
    {
        if (EngineModeCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item)
        {
            return item.Content?.ToString() ?? "TPL";
        }

        return "TPL";
    }

    private RunSettings BuildSettings()
    {
        var workers = ParsePositive(WorkersText.Text, "Wątki/Zadania");
        var cities = ParsePositive(CitiesText.Text, "Miasta");
        var pmx = ParsePositive(PmxSecondsText.Text, "PMX [s]");
        var opt = ParsePositive(OptSecondsText.Text, "3-opt [s]");
        var epochs = ParsePositive(EpochsText.Text, "Epoki");
        var dataPath = ProjectPaths.GetDefaultDataPath();

        return new RunSettings(dataPath, cities, workers, epochs, pmx, opt);
    }

    private static int ParsePositive(string? text, string field)
    {
        if (!int.TryParse(text, out var value) || value <= 0)
        {
            throw new ArgumentException($"Nieprawidłowa wartość pola: {field}");
        }

        return value;
    }

    private void DrawRoute()
    {
        if (_cities is null || _bestRoute is null || TourCanvas.ActualWidth < 20 || TourCanvas.ActualHeight < 20)
        {
            return;
        }

        TourCanvas.Children.Clear();

        var points = _bestRoute
            .Where(i => i >= 0 && i < _cities.Count)
            .Select(i => _cities[i])
            .ToList();

        if (points.Count < 2)
        {
            return;
        }

        var minX = points.Min(c => c.X);
        var maxX = points.Max(c => c.X);
        var minY = points.Min(c => c.Y);
        var maxY = points.Max(c => c.Y);

        var width = TourCanvas.ActualWidth - 16;
        var height = TourCanvas.ActualHeight - 16;
        var spanX = Math.Max(1e-9, maxX - minX);
        var spanY = Math.Max(1e-9, maxY - minY);

        Point Map(City c)
        {
            var x = 8 + ((c.X - minX) / spanX) * width;
            var y = 8 + ((maxY - c.Y) / spanY) * height;
            return new Point(x, y);
        }

        for (var i = 0; i < points.Count; i++)
        {
            var a = Map(points[i]);
            var b = Map(points[(i + 1) % points.Count]);

            TourCanvas.Children.Add(new Line
            {
                X1 = a.X,
                Y1 = a.Y,
                X2 = b.X,
                Y2 = b.Y,
                Stroke = Brushes.DarkBlue,
                StrokeThickness = 1
            });
        }
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawRoute();
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_canClose && _client is { IsRunning: true })
        {
            e.Cancel = true;
            MessageBox.Show("Najpierw zakończ obliczenia (Stop) i poczekaj na completion.", "Obliczenia trwają", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_client is not null)
        {
            try
            {
                await _client.DisposeAsync();
            }
            catch
            {
            }
        }
    }
}