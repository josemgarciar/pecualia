BEGIN;

ALTER TABLE app_user
    ADD COLUMN IF NOT EXISTS task_reminder_enabled BOOLEAN NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS task_reminder_email VARCHAR(255),
    ADD COLUMN IF NOT EXISTS task_reminder_interval_days INTEGER,
    ADD COLUMN IF NOT EXISTS task_reminder_anchor_date DATE,
    ADD COLUMN IF NOT EXISTS task_reminder_last_processed_on DATE,
    ADD COLUMN IF NOT EXISTS task_reminder_last_sent_at TIMESTAMPTZ;

ALTER TABLE app_user
    DROP CONSTRAINT IF EXISTS app_user_task_reminder_interval_positive_chk;

ALTER TABLE app_user
    ADD CONSTRAINT app_user_task_reminder_interval_positive_chk CHECK (
        task_reminder_interval_days IS NULL OR task_reminder_interval_days > 0
    );

COMMIT;
