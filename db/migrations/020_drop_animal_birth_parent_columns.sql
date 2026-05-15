BEGIN;

DROP INDEX IF EXISTS idx_animal_birth_mother_id;

ALTER TABLE animal_birth
    DROP CONSTRAINT IF EXISTS animal_birth_mother_animal_id_fkey,
    DROP CONSTRAINT IF EXISTS animal_birth_father_animal_id_fkey;

ALTER TABLE animal_birth
    DROP COLUMN IF EXISTS mother_animal_id,
    DROP COLUMN IF EXISTS father_animal_id;

COMMIT;
