-- What a household says a barcode is (docs/barcode-scanning-design.md, section 3.1).
--
-- "This Barilla Spaghetti n.5 is Spaghetti, 500 g, 9 minutes": a mapping from a pack's code to an ingredient
-- name, so a scan can match a recipe line or a shopping item. It is not what the household has at home; Culina
-- keeps no stock.
--
-- gtin is the normalised digits (a UPC-A is padded to 13). The row carries a version because two phones can name
-- the same code at the same moment, and the later one must be told rather than silently win.
create table household_products (
    household_id  uuid           not null references households_with_deleted (id) on delete cascade,
    gtin          text           not null check (gtin ~ '^[0-9]{8,14}$'),
    name          text           not null check (length(name) between 1 and 200),
    ingredient    text           null check (length(ingredient) <= 200),
    quantity      numeric(10, 3) null check (quantity > 0),
    unit          text           null check (length(unit) <= 16),
    cook_minutes  integer        null check (cook_minutes between 1 and 600),
    note          text           null check (length(note) <= 500),
    created_by    uuid           null references users (id) on delete set null,
    updated_at    timestamptz    not null,
    version       bigint         not null default 1,

    primary key (household_id, gtin)
);
