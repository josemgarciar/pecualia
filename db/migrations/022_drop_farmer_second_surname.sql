BEGIN;

ALTER TABLE farmer
    DROP COLUMN IF EXISTS second_surname;

COMMIT;
