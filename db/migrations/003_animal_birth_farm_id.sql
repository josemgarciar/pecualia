BEGIN;

ALTER TABLE animal_birth
    ADD COLUMN IF NOT EXISTS livestock_farm_id BIGINT;

UPDATE animal_birth birth
SET livestock_farm_id = animal.livestock_farm_id
FROM animal
WHERE birth.livestock_farm_id IS NULL
  AND birth.mother_animal_id = animal.id;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE table_name = 'animal_birth'
          AND constraint_name = 'animal_birth_livestock_farm_id_fkey'
    ) THEN
        ALTER TABLE animal_birth
            ADD CONSTRAINT animal_birth_livestock_farm_id_fkey
            FOREIGN KEY (livestock_farm_id)
            REFERENCES livestock_farm(id)
            ON DELETE CASCADE;
    END IF;
END $$;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE table_name = 'animal_birth'
          AND constraint_name = 'animal_birth_mother_animal_id_fkey'
    ) THEN
        ALTER TABLE animal_birth
            DROP CONSTRAINT animal_birth_mother_animal_id_fkey;
    END IF;
END $$;

ALTER TABLE animal_birth
    ALTER COLUMN mother_animal_id DROP NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE table_name = 'animal_birth'
          AND constraint_name = 'animal_birth_mother_animal_id_fkey'
    ) THEN
        ALTER TABLE animal_birth
            ADD CONSTRAINT animal_birth_mother_animal_id_fkey
            FOREIGN KEY (mother_animal_id)
            REFERENCES animal(id)
            ON DELETE SET NULL;
    END IF;
END $$;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM animal_birth WHERE livestock_farm_id IS NULL) THEN
        RAISE EXCEPTION 'animal_birth.livestock_farm_id cannot be backfilled for every row';
    END IF;
END $$;

ALTER TABLE animal_birth
    ALTER COLUMN livestock_farm_id SET NOT NULL;

COMMIT;
