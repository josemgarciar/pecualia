ALTER TABLE movement_certificate
    ADD COLUMN IF NOT EXISTS animal_type VARCHAR(80);
