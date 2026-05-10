BEGIN;

ALTER TABLE livestock_farm
    ALTER COLUMN production_capacity TYPE INTEGER
    USING NULLIF(BTRIM(production_capacity::text), '')::INTEGER;

ALTER TABLE livestock_farm
    DROP CONSTRAINT IF EXISTS production_capacity_non_negative_chk;

ALTER TABLE livestock_farm
    ADD CONSTRAINT production_capacity_non_negative_chk
    CHECK (production_capacity IS NULL OR production_capacity >= 0);

COMMIT;
