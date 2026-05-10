BEGIN;

ALTER TABLE movement_certificate
    ADD COLUMN IF NOT EXISTS status VARCHAR(32);

UPDATE movement_certificate
SET status = CASE
    WHEN arrival_date IS NULL THEN 'Pending'
    ELSE 'Confirmed'
END
WHERE status IS NULL;

ALTER TABLE movement_certificate
    ALTER COLUMN status SET NOT NULL,
    ALTER COLUMN status SET DEFAULT 'Pending';

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'movement_status_chk'
    ) THEN
        ALTER TABLE movement_certificate
            ADD CONSTRAINT movement_status_chk CHECK (status IN ('Pending', 'Confirmed'));
    END IF;
END $$;

COMMIT;
