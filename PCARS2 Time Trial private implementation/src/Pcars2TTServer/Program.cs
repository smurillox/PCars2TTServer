using Pcars2TTServer;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<LapRepository>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/laps", async (LapEventRequest request, LapRepository repository, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Gamertag) ||
        string.IsNullOrWhiteSpace(request.Vehicle) ||
        string.IsNullOrWhiteSpace(request.Track) ||
        !request.ValidLap ||
        request.LapTimeMilliseconds <= 0)
    {
        return Results.BadRequest(new { error = "Gamertag, Vehicle, Track, ValidLap=true, and a positive LapTimeMilliseconds are required." });
    }

    var response = await repository.InsertIfNewBestAsync(request, cancellationToken);
    return response.IsNewBest
        ? Results.Created($"/api/laps/{response.Id}", response)
        : Results.Conflict(response);
});

app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        try
        {
            await app.Services.GetRequiredService<LapRepository>().InitializeAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            app.Logger.LogError(exception, "Could not initialize the MySQL schema");
        }
    });
});

app.Run();
