CREATE TABLE recipe_intake_jobs (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    household_id uuid NOT NULL REFERENCES households_with_deleted(id) ON DELETE CASCADE,
    material jsonb NOT NULL,
    stage text NOT NULL DEFAULT 'queued',
    draft jsonb,
    recipe_id uuid REFERENCES recipes_with_deleted(id) ON DELETE SET NULL,
    error_code text,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    lease_until timestamptz,
    attempts integer NOT NULL DEFAULT 0,
    CHECK (stage IN ('queued','reading','thinking','writing','saving','ready','failed','reviewed'))
);
CREATE INDEX recipe_intake_pending ON recipe_intake_jobs(created_at) WHERE stage NOT IN ('ready','failed','reviewed');
CREATE INDEX recipe_intake_owner ON recipe_intake_jobs(user_id, created_at DESC);
CREATE TABLE web_push_identity (
    singleton boolean PRIMARY KEY DEFAULT true CHECK(singleton),
    public_key text NOT NULL,
    private_key text NOT NULL
);
CREATE TABLE web_push_subscriptions (
    endpoint text PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    p256dh text NOT NULL,
    auth text NOT NULL,
    language text NOT NULL DEFAULT 'en',
    created_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE recipe_intake_notifications (
    job_id uuid NOT NULL REFERENCES recipe_intake_jobs(id) ON DELETE CASCADE,
    endpoint text NOT NULL REFERENCES web_push_subscriptions(endpoint) ON DELETE CASCADE,
    delivered_at timestamptz,
    attempts integer NOT NULL DEFAULT 0,
    retry_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (job_id, endpoint)
);
