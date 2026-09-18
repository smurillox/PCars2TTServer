-- One-time cleanup for imported historical Project CARS lap times.
-- Run only after creating a database backup.

START TRANSACTION;

-- Historical rows are assumed valid. Rows without a lap time cannot be a best.
UPDATE laptimes
SET validlap = '1'
WHERE laptime IS NOT NULL;

DROP TEMPORARY TABLE IF EXISTS laptime_winners;

-- Keep the lowest id when multiple rows have the same best time.
CREATE TEMPORARY TABLE laptime_winners AS
SELECT MIN(candidate.id) AS winner_id
FROM laptimes AS candidate
INNER JOIN (
    SELECT
        COALESCE(UPPER(TRIM(gamertag)), '') AS gamertag_key,
        COALESCE(UPPER(TRIM(vehicle)), '') AS vehicle_key,
        COALESCE(UPPER(TRIM(track)), '') AS track_key,
        COALESCE(UPPER(TRIM(game)), '') AS game_key,
        MIN(laptime) AS best_laptime
    FROM laptimes
    WHERE laptime IS NOT NULL
    GROUP BY
        COALESCE(UPPER(TRIM(gamertag)), ''),
        COALESCE(UPPER(TRIM(vehicle)), ''),
        COALESCE(UPPER(TRIM(track)), ''),
        COALESCE(UPPER(TRIM(game)), '')
) AS best
    ON COALESCE(UPPER(TRIM(candidate.gamertag)), '') = best.gamertag_key
   AND COALESCE(UPPER(TRIM(candidate.vehicle)), '') = best.vehicle_key
   AND COALESCE(UPPER(TRIM(candidate.track)), '') = best.track_key
   AND COALESCE(UPPER(TRIM(candidate.game)), '') = best.game_key
   AND candidate.laptime = best.best_laptime
GROUP BY
    best.gamertag_key,
    best.vehicle_key,
    best.track_key,
    best.game_key;

-- Keep only one best row per identity. Rows with NULL laptime are removed.
DELETE laptimes
FROM laptimes
LEFT JOIN laptime_winners
    ON laptimes.id = laptime_winners.winner_id
WHERE laptime_winners.winner_id IS NULL;

DROP TEMPORARY TABLE laptime_winners;

COMMIT;
