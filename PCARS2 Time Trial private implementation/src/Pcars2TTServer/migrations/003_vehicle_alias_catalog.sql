-- Back up the database before running this migration.
-- Seeds canonical PCars2 vehicles from existing history, creates normalized aliases,
-- maps the two catalog spellings that have an obvious historical canonical match,
-- then backfills laptimes.vehicle_id. Other games remain unchanged.

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

ALTER TABLE laptimes
    ADD COLUMN vehicle_id BIGINT NULL AFTER vehicle,
    ADD INDEX ix_laptimes_game_vehicle (game(64), vehicle(128)),
    ADD INDEX ix_laptimes_driver_vehicle_best (game(32), gamertag(96), vehicle_id, track(96), laptime);

-- One canonical row per normalized historical PCars2 vehicle name.
INSERT IGNORE INTO vehicles (game, canonical_name, normalized_name)
SELECT game, MIN(TRIM(vehicle)), REGEXP_REPLACE(UPPER(TRIM(vehicle)), '[^A-Z0-9]', '')
FROM laptimes
WHERE game = 'PCars2' AND vehicle IS NOT NULL AND TRIM(vehicle) <> ''
GROUP BY game, REGEXP_REPLACE(UPPER(TRIM(vehicle)), '[^A-Z0-9]', '');

-- Preserve exact historical spellings as aliases. Names with spacing or punctuation
-- variants normalize to the same vehicle_id.
INSERT IGNORE INTO vehicle_aliases (game, alias_name, normalized_name, vehicle_id)
SELECT source_names.game, source_names.alias_name, source_names.normalized_name, vehicles.vehicle_id
FROM (
    SELECT DISTINCT
        game,
        vehicle AS alias_name,
        REGEXP_REPLACE(UPPER(TRIM(vehicle)), '[^A-Z0-9]', '') AS normalized_name
    FROM laptimes
    WHERE game = 'PCars2' AND vehicle IS NOT NULL AND TRIM(vehicle) <> ''
) AS source_names
INNER JOIN vehicles
    ON vehicles.game = source_names.game
   AND vehicles.normalized_name = source_names.normalized_name;

-- Explicit aliases from the CREST2 catalog that have clear historical counterparts.
INSERT IGNORE INTO vehicle_aliases (game, alias_name, normalized_name, vehicle_id)
SELECT 'PCars2', 'Audi R8 (LMP900)', 'AUDIR8LMP900', vehicle_id
FROM vehicles
WHERE game = 'PCars2' AND normalized_name = 'AUDIR8';

INSERT IGNORE INTO vehicle_aliases (game, alias_name, normalized_name, vehicle_id)
SELECT 'PCars2', 'Nissan GT-R Nismo (R35)', 'NISSANGTRNISMOR35', vehicle_id
FROM vehicles
WHERE game = 'PCars2' AND normalized_name = 'NISSANGTRNISMO';

-- Backfill by exact raw historical alias; the supporting game/vehicle index avoids
-- a repeated full-table expression join.
UPDATE laptimes
INNER JOIN vehicle_aliases
    ON vehicle_aliases.game = laptimes.game
   AND vehicle_aliases.alias_name = laptimes.vehicle
SET laptimes.vehicle_id = vehicle_aliases.vehicle_id
WHERE laptimes.game = 'PCars2'
  AND laptimes.vehicle_id IS NULL
  AND vehicle_aliases.vehicle_id IS NOT NULL;

-- Review any rows left unmapped before enabling the updated backend.
SELECT game, vehicle, COUNT(*) AS rows_without_vehicle_id
FROM laptimes
WHERE game = 'PCars2' AND vehicle_id IS NULL
GROUP BY game, vehicle
ORDER BY vehicle;
