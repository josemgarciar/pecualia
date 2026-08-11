CREATE TABLE IF NOT EXISTS animal_bulk_update_operation (
    id UUID PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES app_user(id) ON DELETE CASCADE,
    farm_id BIGINT NOT NULL REFERENCES livestock_farm(id) ON DELETE CASCADE,
    request_hash VARCHAR(64) NOT NULL,
    state VARCHAR(24) NOT NULL,
    result_json JSONB,
    created_at TIMESTAMPTZ NOT NULL,
    completed_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_animal_bulk_update_operation_owner
    ON animal_bulk_update_operation(user_id, farm_id, created_at DESC);

CREATE UNIQUE INDEX IF NOT EXISTS uq_movement_official_entry
    ON movement_certificate(
        destination_livestock_id,
        UPPER(BTRIM(cod_remo)),
        UPPER(BTRIM(serie))
    )
    WHERE destination_livestock_id IS NOT NULL
      AND cod_remo IS NOT NULL AND BTRIM(cod_remo) <> ''
      AND serie IS NOT NULL AND BTRIM(serie) <> '';

CREATE UNIQUE INDEX IF NOT EXISTS uq_movement_official_exit
    ON movement_certificate(
        origin_livestock_id,
        UPPER(BTRIM(cod_remo)),
        UPPER(BTRIM(serie))
    )
    WHERE origin_livestock_id IS NOT NULL
      AND cod_remo IS NOT NULL AND BTRIM(cod_remo) <> ''
      AND serie IS NOT NULL AND BTRIM(serie) <> '';
