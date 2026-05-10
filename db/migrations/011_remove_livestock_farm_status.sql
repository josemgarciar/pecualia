BEGIN;

ALTER TABLE livestock_farm
    DROP COLUMN IF EXISTS status;

COMMIT;
