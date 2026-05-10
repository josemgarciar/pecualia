BEGIN;

ALTER TABLE animal_birth
    ADD COLUMN IF NOT EXISTS balance_id BIGINT REFERENCES balance(id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS idx_animal_birth_balance_id ON animal_birth(balance_id);

COMMIT;
