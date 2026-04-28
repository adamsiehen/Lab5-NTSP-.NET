using Lab5.Worker.ThreadPool;
using Lab5.Shared;

await WorkerServerHost.RunAsync(
	args,
	"Usage: Lab5.Worker.ThreadPool <pipe-name>",
	ThreadPoolWorkerEngine.RunAsync);
