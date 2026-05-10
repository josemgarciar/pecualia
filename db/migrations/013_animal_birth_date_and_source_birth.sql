BEGIN;

ALTER TABLE animal
    ADD COLUMN IF NOT EXISTS birth_date DATE;

ALTER TABLE animal
    ADD COLUMN IF NOT EXISTS source_birth_id BIGINT;

UPDATE animal
SET birth_date = make_date(birth_year, 1, 1)
WHERE birth_date IS NULL
  AND birth_year IS NOT NULL
  AND birth_year BETWEEN 1900 AND 2100;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE table_name = 'animal'
          AND constraint_name = 'animal_source_birth_id_fkey'
    ) THEN
        ALTER TABLE animal
            ADD CONSTRAINT animal_source_birth_id_fkey
            FOREIGN KEY (source_birth_id) REFERENCES animal_birth(id) ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_animal_source_birth_id ON animal(source_birth_id);

COMMIT;
