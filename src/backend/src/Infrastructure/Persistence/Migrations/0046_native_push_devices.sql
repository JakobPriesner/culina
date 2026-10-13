-- The installed apps' push devices, reached through the publisher's relay
-- (docs/native-app-design.md, part 7).
--
-- A device holds a relay token: the relay's stand-in for the phone's APNs or
-- FCM token, which this server never sees. Holding it is the permission to
-- push to that phone, as holding a Web Push endpoint is, so it lives here and
-- on the phone only. The app picks the device id, so signing in again on the
-- same phone replaces the row rather than adding one.
create table native_push_devices (
    device_id   uuid        primary key,
    user_id     uuid        not null references users (id) on delete cascade,
    relay_token text        not null check (length(relay_token) between 1 and 200),
    language    text        not null default 'en' check (language in ('en', 'de')),
    created_at  timestamptz not null default now()
);

create index native_push_devices_user_idx on native_push_devices (user_id);

-- The outbox for devices, beside recipe_intake_notifications for Web Push and
-- delivered by the same claim and retry loop. Its own table because a row
-- names a device, not a subscription endpoint; a device dropped by the relay
-- takes its pending rows with it.
create table recipe_intake_device_notifications (
    job_id       uuid        not null references recipe_intake_jobs (id) on delete cascade,
    device_id    uuid        not null references native_push_devices (device_id) on delete cascade,
    delivered_at timestamptz,
    attempts     integer     not null default 0,
    retry_at     timestamptz not null default now(),
    primary key (job_id, device_id)
);
