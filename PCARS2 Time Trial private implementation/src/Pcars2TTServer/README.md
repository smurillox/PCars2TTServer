# PCARS2 Time Trial backend

HTTP API for receiving valid lap events from the Windows collector and keeping only each user's best lap in the existing `laptimes` MySQL table.

## API

- `GET /health`
- `POST /api/laps`

Example request:

```json
{
  "gamertag": "Test Driver",
  "vehicle": "BMW M3 GT",
  "vehicleClass": "GT3",
  "track": "Azure Circuit / Grand Prix",
  "game": "Project CARS 2",
  "sessionMode": "Time Attack",
  "validLap": true,
  "lapTimeMilliseconds": 91234,
  "capturedAt": "2026-09-17T12:00:00Z",
  "sourceTimestamp": 1234567890
}
```

The server uses the supplied `laptimes` schema. It stores `lapTimeMilliseconds / 1000` in the schema's `laptime` `REAL` column. The comparison key is normalized `gamertag + vehicle + track + game`.

Only a strictly faster valid lap is accepted. A slower or equal lap returns `409 Conflict` and leaves the existing row unchanged. An accepted improvement deletes all older matching rows and inserts the new best in one transaction.

## Fedora / Podman

Set the MySQL passwords, then run from the repository root:

```bash
export MYSQL_PASSWORD='change-me'
export MYSQL_ROOT_PASSWORD='change-root-me'
podman compose -f src/Pcars2TTServer/compose.yml up --build -d
curl http://127.0.0.1:8080/health
```

The compose file runs MySQL and the API. MySQL data persists in the Podman volume `pcars2ttserver_mysql-data` and is not exposed directly to the network. The service listens on all container interfaces at port `8080`.

To import an existing SQL dump after copying it to Fedora with WinSCP:

```bash
podman cp pcars2tt.sql pcars2tt-mysql:/tmp/pcars2tt.sql
podman exec -i pcars2tt-mysql mysql -u root -p"$MYSQL_ROOT_PASSWORD" pcars2tt < pcars2tt.sql
```

The imported dump should create the `laptimes` table before the backend receives events. The backend also creates the table if it is absent.

After importing and backing up the database, run the one-time historical cleanup:

```bash
podman exec -i pcars2tt-mysql \
  mysql -u root -p"$MYSQL_ROOT_PASSWORD" pcars2tt \
  < src/Pcars2TTServer/migrations/001_dedupe_laptimes.sql
```

This marks existing rows with a lap time as valid, keeps the fastest row for each normalized `gamertag + vehicle + track + game` combination, and removes rows without a lap time. A new combination is still inserted normally by the API.
