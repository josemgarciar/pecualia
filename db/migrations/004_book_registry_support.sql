BEGIN;

ALTER TABLE livestock_farm
    ADD COLUMN IF NOT EXISTS porcine_registry_number VARCHAR(32);

COMMIT;
