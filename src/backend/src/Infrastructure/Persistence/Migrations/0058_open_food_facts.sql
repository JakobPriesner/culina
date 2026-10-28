-- Open Food Facts, looked up by barcode when the administrator switched it on.
--
-- product_lookups is the server's cache of a third party, not household data:
-- only codes somebody scanned or found by an explicit search, the few fields
-- Culina uses, and "Open Food Facts does not know this code" too, so an
-- own-brand item is not asked for again on every scan. Hits are asked again
-- after 30 days, misses after 7. Its rows are Open Food Facts' data under the
-- ODbL and are never copied into household tables: a household food made from
-- one is the person's own confirmed act (docs/app-structure-design.md §6.3).
create table product_lookups (
    gtin        text        primary key check (gtin ~ '^[0-9]{8,14}$'),
    found       boolean     not null,
    product     jsonb,
    fetched_at  timestamptz not null,
    check (found = (product is not null))
);

comment on table product_lookups is
    'Open Food Facts answers by barcode (ODbL), a cache of codes somebody asked for; never merged into household data.';

-- A household food can now come from a scanned package: which barcode, and
-- whether its values are the ones Open Food Facts gave or the person changed
-- them. 'package' is typed from the label, as every food before this.
alter table household_foods
    add column gtin   text check (gtin ~ '^[0-9]{8,14}$'),
    add column source text not null default 'package'
        check (source in ('package', 'openFoodFacts', 'openFoodFactsEdited'));

create index household_foods_gtin on household_foods (household_id, gtin) where gtin is not null;
