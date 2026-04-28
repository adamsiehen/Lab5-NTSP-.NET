using Lab5.Worker.Tpl;
using Lab5.Shared;

await WorkerServerHost.RunAsync(
	args,
	"Usage: Lab5.Worker.Tpl <pipe-name>",
	TplWorkerEngine.RunAsync);
