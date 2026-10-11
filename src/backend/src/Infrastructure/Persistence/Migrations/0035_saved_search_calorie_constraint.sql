alter table saved_searches drop constraint saved_searches_ask_something;
alter table saved_searches add constraint saved_searches_ask_something check (
    (query is not null and length(btrim(query)) > 0)
    or cardinality(tags) > 0
    or max_minutes is not null
    or max_kcal is not null
    or sort is not null);
