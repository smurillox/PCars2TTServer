-- Normalize imported track display values after the backend separator fix.
UPDATE laptimes
SET track = REPLACE(REPLACE(TRIM(track), ' / ', '-'), '/', '-')
WHERE track IS NOT NULL;