BEGIN;

ALTER TABLE animal
    DROP COLUMN IF EXISTS health_document_number;

ALTER TABLE balance
    DROP COLUMN IF EXISTS health_document_number;

COMMIT;
