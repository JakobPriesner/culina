-- Indexes for the intake outboxes, and for the daily purge that now keeps them
-- from growing for ever.
--
-- The device outbox is polled every couple of seconds for due, undelivered rows,
-- as the Web Push outbox is (0029), and neither outbox could be searched by the
-- subscription or device it belongs to: forgetting a device on sign-out or a
-- subscription on a 410 read the whole table. Finished and failed jobs are
-- found by when they last changed.
create index recipe_intake_device_notifications_due_idx
    on recipe_intake_device_notifications (retry_at) where delivered_at is null;
create index recipe_intake_device_notifications_device_idx
    on recipe_intake_device_notifications (device_id);
create index recipe_intake_notifications_endpoint_idx
    on recipe_intake_notifications (endpoint);
create index recipe_intake_jobs_settled_idx
    on recipe_intake_jobs (updated_at) where stage in ('reviewed', 'failed');
