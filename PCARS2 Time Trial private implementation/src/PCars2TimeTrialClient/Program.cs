using System.Windows.Forms;
using Pcars2Collector;
using PCars2TimeTrialClient;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("clientsettings.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddSingleton<CollectorStatus>();
builder.Services.AddSingleton<ITelemetrySource, NativeSharedMemoryTelemetrySource>();
builder.Services.AddSingleton(services => new CollectorForm(
    services.GetRequiredService<CollectorStatus>(),
    catalogMode: false,
    telemetrySourceName: "$pcars2$ shared memory",
    collectorTitle: "PCars2TimeTrialClient"));
builder.Services.AddHttpClient<ILapEventSink, HttpLapEventSink>(client =>
{
    var baseUrl = builder.Configuration["Pcars2:BackendBaseUrl"] ?? "http://127.0.0.1:8080/";
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddSingleton<LapDetector>();
builder.Services.AddSingleton<LapEventFileWriter>();
builder.Services.AddHostedService<CollectorWorker>();

using var host = builder.Build();
await host.StartAsync();
Application.Run(host.Services.GetRequiredService<CollectorForm>());
await host.StopAsync();
