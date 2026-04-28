namespace Lab5.Shared;

public sealed record City(int Id, double X, double Y);

public sealed record RouteCandidate(int[] Order, double Length, int ProducerId);

public sealed record RunSettings(
    string DataFilePath,
    int CityCount,
    int Workers,
    int Epochs,
    int PmxSeconds,
    int OptSeconds
);