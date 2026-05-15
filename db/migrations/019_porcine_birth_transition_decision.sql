CREATE TABLE porcine_birth_transition_decision (
    birth_id BIGINT PRIMARY KEY REFERENCES animal_birth(id) ON DELETE CASCADE,
    effective_date DATE NOT NULL,
    to_rears INTEGER NOT NULL DEFAULT 0,
    to_sows_reposition INTEGER NOT NULL DEFAULT 0,
    to_males_reposition INTEGER NOT NULL DEFAULT 0,
    baseline_rears_consumed INTEGER NOT NULL DEFAULT 0,
    baseline_sows_reposition_consumed INTEGER NOT NULL DEFAULT 0,
    baseline_males_reposition_consumed INTEGER NOT NULL DEFAULT 0,
    resolved_at TIMESTAMPTZ NOT NULL,
    balance_id BIGINT,
    CONSTRAINT porcine_birth_transition_decision_non_negative_chk CHECK (
        to_rears >= 0
        AND to_sows_reposition >= 0
        AND to_males_reposition >= 0
        AND baseline_rears_consumed >= 0
        AND baseline_sows_reposition_consumed >= 0
        AND baseline_males_reposition_consumed >= 0
    )
);

ALTER TABLE porcine_birth_transition_decision
    ADD CONSTRAINT porcine_birth_transition_decision_balance_id_fkey
    FOREIGN KEY (balance_id) REFERENCES balance(id) ON DELETE SET NULL;
