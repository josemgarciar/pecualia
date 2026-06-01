BEGIN;

DO $migration$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'livestock_farm'
          AND column_name = 'production_capacity'
    ) THEN
        ALTER TABLE livestock_farm
            ALTER COLUMN production_capacity TYPE INTEGER
            USING NULLIF(BTRIM(production_capacity::text), '')::INTEGER;

        ALTER TABLE livestock_farm
            DROP CONSTRAINT IF EXISTS production_capacity_non_negative_chk;

        ALTER TABLE livestock_farm
            ADD CONSTRAINT production_capacity_non_negative_chk
            CHECK (production_capacity IS NULL OR production_capacity >= 0);
    END IF;
END
$migration$;

COMMIT;
