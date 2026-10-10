# PCARS2 Time Trial backend

HTTP API for receiving valid lap events from the Windows collector and keeping only each user's best lap in the existing `laptimes` MySQL table.

## API

- `GET /health`
- `POST /api/laps`
- `GET /api/laps/options`
- `GET /api/laps?track=...&vehicle=...&vehicleClass=...&gamertag=...`

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

For vehicle identity, the server resolves the game's raw vehicle name through `vehicle_aliases` to a stable `vehicles.vehicle_id`. Historical `laptimes.vehicle` remains populated with the canonical display name for compatibility, while `laptimes.vehicle_id` is the identity used for best-lap comparison. An unmapped vehicle is recorded as a pending alias and its lap is rejected with HTTP 422; no guessed vehicle record is created.

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

## Vehicle catalog migration

Back up MySQL first, then stop the API and run the one-time migration. It seeds canonical PCars2 vehicles and aliases from historical names, adds/backfills `laptimes.vehicle_id`, and maps two high-confidence catalog aliases (`Audi R8 (LMP900)` to `Audi R8`, and `Nissan GT-R Nismo (R35)` to `Nissan GT-R Nismo`). The query at the end lists historical rows that did not resolve; inspect those before starting the updated API.

```bash
podman compose -f src/Pcars2TTServer/compose.yml stop pcars2tt-server
podman cp src/Pcars2TTServer/migrations/003_vehicle_alias_catalog.sql \
  pcars2tt-mysql:/tmp/003_vehicle_alias_catalog.sql
podman exec -it pcars2tt-mysql mysql -u root -p pcars2tt
```

At the MySQL `mysql>` prompt, run:

```sql
SOURCE /tmp/003_vehicle_alias_catalog.sql;
```

Wait for the migration to finish, then exit MySQL with `\q`. Enter the MySQL password only at its prompt. Copying the script into the container first lets `SOURCE` read it without piping over the password prompt.

Back at Fedora's shell, start the updated API:

```bash
podman compose -f src/Pcars2TTServer/compose.yml up --build --force-recreate -d pcars2tt-server
```

The migration updates the 1.2M-row PCars2 lap table. It creates an index first and backfills using exact indexed aliases; allow it to finish and do not run it a second time because the `ALTER TABLE` section is intentionally one-shot. Review pending live names with:

```sql
SELECT game, alias_name, normalized_name, observation_count, last_seen_at
FROM vehicle_aliases
WHERE vehicle_id IS NULL
ORDER BY game, alias_name;
```

The web GUI runs as a separate container on port `8081` and proxies API requests internally:

```text
http://<fedora-ip>:8081
```

After importing and backing up the database, run the one-time historical cleanup:

```bash
podman exec -i pcars2tt-mysql \
  mysql -u root -p"$MYSQL_ROOT_PASSWORD" pcars2tt \
  < src/Pcars2TTServer/migrations/001_dedupe_laptimes.sql
```

This marks existing rows with a lap time as valid, keeps the fastest row for each normalized `gamertag + vehicle + track + game` combination, and removes rows without a lap time. A new combination is still inserted normally by the API.

To normalize existing slash-separated track values after the separator fix:

```bash
podman exec -i pcars2tt-mysql \
  mysql -u root -p"$MYSQL_ROOT_PASSWORD" pcars2tt \
  < src/Pcars2TTServer/migrations/002_normalize_track_separators.sql
```
