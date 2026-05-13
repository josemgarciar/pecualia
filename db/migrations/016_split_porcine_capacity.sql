BEGIN;

ALTER TABLE livestock_farm
    ADD COLUMN IF NOT EXISTS porcine_mothers_capacity INTEGER,
    ADD COLUMN IF NOT EXISTS porcine_fattening_capacity INTEGER;

ALTER TABLE livestock_farm
    DROP CONSTRAINT IF EXISTS porcine_mothers_capacity_non_negative_chk;

ALTER TABLE livestock_farm
    ADD CONSTRAINT porcine_mothers_capacity_non_negative_chk
    CHECK (porcine_mothers_capacity IS NULL OR porcine_mothers_capacity >= 0);

ALTER TABLE livestock_farm
    DROP CONSTRAINT IF EXISTS porcine_fattening_capacity_non_negative_chk;

ALTER TABLE livestock_farm
    ADD CONSTRAINT porcine_fattening_capacity_non_negative_chk
    CHECK (porcine_fattening_capacity IS NULL OR porcine_fattening_capacity >= 0);

COMMIT;
