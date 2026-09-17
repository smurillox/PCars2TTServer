using MySqlConnector;

namespace Pcars2TTServer;

public sealed class LapRepository(IConfiguration configuration)
{
    private readonly string connectionString = configuration.GetConnectionString("MySql")
        ?? throw new InvalidOperationException("ConnectionStrings:MySql is required.");

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS laptimes (
                id INTEGER PRIMARY KEY AUTO_INCREMENT,
                gamertag TEXT NULL,
                vehicle TEXT NULL,
                vehicleclass TEXT NULL,
                track TEXT NULL,
                laptime REAL NULL,
                sector1 REAL NULL,
                sector2 REAL NULL,
                sector3 REAL NULL,
                lapdate TEXT NULL,
                sessionmode TEXT NULL,
                validlap TEXT NULL,
                setup TEXT NULL,
                controller TEXT NULL,
                game TEXT NULL DEFAULT NULL,
                INDEX vehiculos (game(100), vehicleclass(100), vehicle(100)),
                INDEX tracks (game(100), track(100))
            ) ENGINE=InnoDB;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<LapEventResponse> InsertIfNewBestAsync(LapEventRequest request, CancellationToken cancellationToken)
    {
        var gamertag = LapKey.Normalize(request.Gamertag);
        var vehicle = LapKey.Normalize(request.Vehicle);
        var track = LapKey.Normalize(request.Track);
        var game = LapKey.Normalize(request.Game);
        var lapTimeSeconds = request.LapTimeMilliseconds / 1000.0;

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var bestCommand = connection.CreateCommand();
        bestCommand.Transaction = transaction;
        bestCommand.CommandText = """
            SELECT id, laptime
            FROM laptimes
            WHERE gamertag = @gamertag
              AND vehicle = @vehicle
              AND track = @track
              AND game = @game
              AND validlap IN ('1', 'true', 'TRUE')
            ORDER BY laptime ASC
            LIMIT 1
            FOR UPDATE;
            """;
        AddKeyParameters(bestCommand, gamertag, vehicle, track, game);

        long? previousBestId = null;
        long? previousBestMilliseconds = null;
        await using (var reader = await bestCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                previousBestId = reader.GetInt64(0);
                previousBestMilliseconds = (long)Math.Round(reader.GetDouble(1) * 1000, MidpointRounding.AwayFromZero);
            }
        }

        if (previousBestMilliseconds is not null && lapTimeSeconds >= previousBestMilliseconds.Value / 1000.0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new LapEventResponse(previousBestId!.Value, gamertag, track,
                request.LapTimeMilliseconds, previousBestMilliseconds, false, request.CapturedAt);
        }

        await using var deleteCommand = connection.CreateCommand();
        deleteCommand.Transaction = transaction;
        deleteCommand.CommandText = """
            DELETE FROM laptimes
            WHERE gamertag = @gamertag AND vehicle = @vehicle AND track = @track AND game = @game;
            """;
        AddKeyParameters(deleteCommand, gamertag, vehicle, track, game);
        await deleteCommand.ExecuteNonQueryAsync(cancellationToken);

        await using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = """
            INSERT INTO laptimes
                (gamertag, vehicle, vehicleclass, track, laptime, lapdate,
                 sessionmode, validlap, game)
            VALUES
                (@gamertag, @vehicle, @vehicleclass, @track, @laptime, @lapdate,
                 @sessionmode, '1', @game);
            SELECT LAST_INSERT_ID();
            """;
        insertCommand.Parameters.AddWithValue("@gamertag", gamertag);
        insertCommand.Parameters.AddWithValue("@vehicle", vehicle);
        insertCommand.Parameters.AddWithValue("@vehicleclass", request.VehicleClass.Trim());
        insertCommand.Parameters.AddWithValue("@track", track);
        insertCommand.Parameters.AddWithValue("@laptime", lapTimeSeconds);
        insertCommand.Parameters.AddWithValue("@lapdate", request.CapturedAt.UtcDateTime.ToString("O"));
        insertCommand.Parameters.AddWithValue("@sessionmode", request.SessionMode.Trim());
        insertCommand.Parameters.AddWithValue("@game", game);
        var id = Convert.ToInt64(await insertCommand.ExecuteScalarAsync(cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        return new LapEventResponse(id, gamertag, track, request.LapTimeMilliseconds,
            previousBestMilliseconds, true, request.CapturedAt);
    }

    private static void AddKeyParameters(MySqlCommand command, string gamertag, string vehicle, string track, string game)
    {
        command.Parameters.AddWithValue("@gamertag", gamertag);
        command.Parameters.AddWithValue("@vehicle", vehicle);
        command.Parameters.AddWithValue("@track", track);
        command.Parameters.AddWithValue("@game", game);
    }
}
