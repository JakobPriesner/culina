-- A session displaced because the cook started another recipe (the side dish while
-- the main simmers) is set aside, not given up: starting its recipe again soon after
-- picks it up at the step it was on. A session ended on purpose stays ended.
alter table cook_sessions add column set_aside_at timestamptz;
