-- Whose price a call's cost was worked out with.
--
-- Culina ships a list of model prices that goes stale, and an administrator can
-- now set their own for the models they use. A cost is only worth comparing with
-- an invoice if the screen can say which of the two it came from, and a price
-- changed mid-month must not relabel the calls already counted, so it is
-- recorded per call rather than looked up when the total is shown.
alter table assistance_usage add column priced_by text
    check (priced_by in ('culina', 'administrator'));

-- Every cost recorded so far came from the list in the code: there was nothing
-- else for it to come from.
update assistance_usage set priced_by = 'culina' where cost is not null;

comment on column assistance_usage.priced_by is
    '''culina'' for the price list in the code, ''administrator'' for a price set on the settings screen; null exactly when cost is null.';
