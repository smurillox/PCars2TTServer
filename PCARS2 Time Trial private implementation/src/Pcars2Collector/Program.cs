using System.Windows.Forms;
using Pcars2Collector;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient<ITelemetrySource, Crest2Client>(client =>
{
    var baseUrl = builder.Configuration["Pcars2:Crest2BaseUrl"] ?? "http://127.0.0.1:8180/";
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromMilliseconds(500);
});
builder.Services.AddHttpClient<ILapEventSink, HttpLapEventSink>(client =>
{
    var baseUrl = builder.Configuration["Pcars2:BackendBaseUrl"] ?? "http://127.0.0.1:8080/";
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddSingleton<LapDetector>();
builder.Services.AddSingleton<LapEventFileWriter>();
builder.Services.AddSingleton<CollectorStatus>();
builder.Services.AddSingleton<CollectorForm>();
builder.Services.AddHostedService<CollectorWorker>();

using var host = builder.Build();
await host.StartAsync();

var form = host.Services.GetRequiredService<CollectorForm>();
Application.Run(form);

await host.StopAsync();
