CREATE TABLE IF NOT EXISTS movement_birth_lot (
    movement_certificate_id BIGINT NOT NULL REFERENCES movement_certificate(id) ON DELETE CASCADE,
    birth_id BIGINT NOT NULL REFERENCES animal_birth(id) ON DELETE RESTRICT,
    quantity INTEGER NOT NULL CHECK (quantity > 0),
    PRIMARY KEY (movement_certificate_id, birth_id)
);
CREATE INDEX IF NOT EXISTS ix_movement_birth_lot_birth ON movement_birth_lot(birth_id);

ALTER TABLE movement_certificate DROP CONSTRAINT IF EXISTS movement_unidentified_category_chk;
ALTER TABLE movement_certificate ADD CONSTRAINT movement_unidentified_category_chk CHECK (
    unidentified_category IS NULL OR unidentified_category IN ('Under4Months', 'Between4And12Months', 'BirthLots')
);
