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
                vehicle_id BIGINT NULL,
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
                INDEX ix_laptimes_driver_vehicle_best (game(32), gamertag(96), vehicle_id, track(96), laptime),
                INDEX tracks (game(100), track(100))
            ) ENGINE=InnoDB;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await using var vehicleCatalogCommand = connection.CreateCommand();
        vehicleCatalogCommand.CommandText = """
            CREATE TABLE IF NOT EXISTS vehicles (
                vehicle_id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                game VARCHAR(64) NOT NULL,
                canonical_name VARCHAR(255) NOT NULL,
                normalized_name VARCHAR(255) NOT NULL,
                created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
                UNIQUE KEY uq_vehicles_game_normalized (game, normalized_name)
            ) ENGINE=InnoDB;

            CREATE TABLE IF NOT EXISTS vehicle_aliases (
                alias_id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                game VARCHAR(64) NOT NULL,
                alias_name VARCHAR(255) NOT NULL,
                normalized_name VARCHAR(255) NOT NULL,
                vehicle_id BIGINT NULL,
                first_seen_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
                last_seen_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
                observation_count BIGINT NOT NULL DEFAULT 1,
                UNIQUE KEY uq_vehicle_alias_game_name (game, alias_name),
                KEY ix_vehicle_alias_normalized (game, normalized_name),
                CONSTRAINT fk_vehicle_alias_vehicle FOREIGN KEY (vehicle_id) REFERENCES vehicles(vehicle_id)
            ) ENGINE=InnoDB;
            """;
        await vehicleCatalogCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<LapEventResponse> InsertIfNewBestAsync(LapEventRequest request, CancellationToken cancellationToken)
    {
        var gamertag = LapKey.Normalize(request.Gamertag);
        var track = LapKey.CanonicalTrack(request.Track);
        var game = request.Game.Trim();
        var lapTimeSeconds = request.LapTimeMilliseconds / 1000.0;

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var resolvedVehicle = await ResolveVehicleAsync(connection, game, request.Vehicle, cancellationToken);
        if (resolvedVehicle is null)
        {
            await RecordUnmappedVehicleAsync(connection, game, request.Vehicle, cancellationToken);
            throw new UnknownVehicleAliasException(game, request.Vehicle);
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var bestCommand = connection.CreateCommand();
        bestCommand.Transaction = transaction;
        bestCommand.CommandText = """
            SELECT id, laptime
            FROM laptimes
            WHERE gamertag = @gamertag
                            AND vehicle_id = @vehicle_id
              AND UPPER(REPLACE(REPLACE(TRIM(track), ' / ', '-'), '/', '-')) = UPPER(@track)
              AND game = @game
              AND validlap IN ('1', 'true', 'TRUE')
            ORDER BY laptime ASC
            LIMIT 1
            FOR UPDATE;
            """;
        AddKeyParameters(bestCommand, gamertag, resolvedVehicle.Id, track, game);

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
                        WHERE gamertag = @gamertag
                            AND vehicle_id = @vehicle_id
                              AND UPPER(REPLACE(REPLACE(TRIM(track), ' / ', '-'), '/', '-')) = UPPER(@track)
                            AND game = @game;
            """;
        AddKeyParameters(deleteCommand, gamertag, resolvedVehicle.Id, track, game);
        await deleteCommand.ExecuteNonQueryAsync(cancellationToken);

        await using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = """
            INSERT INTO laptimes
                (gamertag, vehicle, vehicle_id, vehicleclass, track, laptime, lapdate,
                 sessionmode, validlap, game)
            VALUES
                (@gamertag, @vehicle, @vehicle_id, @vehicleclass, @track, @laptime, @lapdate,
                 @sessionmode, '1', @game);
            SELECT LAST_INSERT_ID();
            """;
        insertCommand.Parameters.AddWithValue("@gamertag", gamertag);
        insertCommand.Parameters.AddWithValue("@vehicle", resolvedVehicle.CanonicalName);
        insertCommand.Parameters.AddWithValue("@vehicle_id", resolvedVehicle.Id);
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

    private static async Task<ResolvedVehicle?> ResolveVehicleAsync(
        MySqlConnection connection,
        string game,
        string rawVehicleName,
        CancellationToken cancellationToken)
    {
        var normalizedName = VehicleKey.Normalize(rawVehicleName);
        if (normalizedName.Length == 0)
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT v.vehicle_id, v.canonical_name
            FROM vehicle_aliases AS a
            INNER JOIN vehicles AS v ON v.vehicle_id = a.vehicle_id
            WHERE a.game = @game AND a.normalized_name = @normalized_name
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@game", game);
        command.Parameters.AddWithValue("@normalized_name", normalizedName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ResolvedVehicle(reader.GetInt64(0), reader.GetString(1))
            : null;
    }

    private static async Task RecordUnmappedVehicleAsync(
        MySqlConnection connection,
        string game,
        string rawVehicleName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO vehicle_aliases (game, alias_name, normalized_name, vehicle_id)
            VALUES (@game, @alias_name, @normalized_name, NULL)
            ON DUPLICATE KEY UPDATE
                last_seen_at = CURRENT_TIMESTAMP(6),
                observation_count = observation_count + 1;
            """;
        command.Parameters.AddWithValue("@game", game);
        command.Parameters.AddWithValue("@alias_name", rawVehicleName.Trim());
        command.Parameters.AddWithValue("@normalized_name", VehicleKey.Normalize(rawVehicleName));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<LapFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var tracks = await GetDistinctValuesAsync(connection, "track", cancellationToken);
        var vehicles = await GetDistinctValuesAsync(connection, "vehicle", cancellationToken);
        var classes = await GetDistinctValuesAsync(connection, "vehicleclass", cancellationToken);
        var gamertags = await GetDistinctValuesAsync(connection, "gamertag", cancellationToken);
        return new LapFilterOptions(tracks, vehicles, classes, gamertags);
    }

    public async Task<IReadOnlyList<LapRecord>> QueryAsync(LapQuery query, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var filters = new List<string> { "laptime IS NOT NULL" };
        AddOptionalTrackFilter(command, filters, query.Track);
        AddOptionalFilter(command, filters, "vehicle", "@vehicle", query.Vehicle);
        AddOptionalFilter(command, filters, "vehicleclass", "@vehicleclass", query.VehicleClass);
        AddOptionalFilter(command, filters, "gamertag", "@gamertag", query.Gamertag);

        command.CommandText = $"""
            SELECT id, gamertag, vehicle, vehicleclass, track, laptime, lapdate, sessionmode, game
            FROM laptimes
            WHERE {string.Join(" AND ", filters)}
            ORDER BY laptime ASC, id ASC
            LIMIT @limit;
            """;
        command.Parameters.AddWithValue("@limit", Math.Clamp(query.Limit, 1, 1000));

        var records = new List<LapRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new LapRecord(
                reader.GetInt64(0),
                ReadString(reader, 1),
                ReadString(reader, 2),
                ReadString(reader, 3),
                ReadString(reader, 4),
                (long)Math.Round(reader.GetDouble(5) * 1000, MidpointRounding.AwayFromZero),
                ParseDate(ReadString(reader, 6)),
                ReadString(reader, 7),
                ReadString(reader, 8)));
        }

        return records;
    }

    private static async Task<IReadOnlyList<string>> GetDistinctValuesAsync(
        MySqlConnection connection,
        string column,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT DISTINCT `{column}` FROM laptimes WHERE `{column}` IS NOT NULL AND TRIM(`{column}`) <> '' ORDER BY `{column}`;";
        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    private static void AddOptionalFilter(MySqlCommand command, List<string> filters, string column, string parameter, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            filters.Add($"`{column}` = {parameter}");
            command.Parameters.AddWithValue(parameter, value.Trim());
        }
    }

    private static void AddOptionalTrackFilter(MySqlCommand command, List<string> filters, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            filters.Add("UPPER(REPLACE(REPLACE(TRIM(`track`), ' / ', '-'), '/', '-')) = @track");
            command.Parameters.AddWithValue("@track", LapKey.CanonicalTrack(value));
        }
    }

    private static string ReadString(MySqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? "" : reader.GetString(ordinal);

    private static DateTimeOffset? ParseDate(string value) =>
        DateTimeOffset.TryParse(value, out var date) ? date : null;

    private static void AddKeyParameters(MySqlCommand command, string gamertag, long vehicleId, string track, string game)
    {
        command.Parameters.AddWithValue("@gamertag", gamertag);
        command.Parameters.AddWithValue("@vehicle_id", vehicleId);
        command.Parameters.AddWithValue("@track", track);
        command.Parameters.AddWithValue("@game", game);
    }
}
