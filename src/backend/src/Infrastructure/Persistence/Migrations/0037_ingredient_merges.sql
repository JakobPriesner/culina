-- A household's standing decision that one ingredient name is another.
--
-- Names are free text on every recipe line, so one ingredient drifts into
-- several: "Hühnchenbrustfilet" next to "Hähnchenbrust", "Olivenoel" next to
-- "Olivenöl". Merging rewrites the household's own recipes once, and the rule
-- stays so that every later save, import, copy or restore writes the kept name
-- too; otherwise the next import brings the old one straight back.
--
-- from_key and to_key are the folded names (ItemName.Fold), the same key the
-- nutrition and shopping overrides use. A rule may fold to its own target
-- ("Olivenoel" -> "Olivenöl"): that settles a spelling. Rules are kept flat:
-- merging into a name that is itself merged follows it, and rules that pointed
-- at a name which is merged away are pointed on, so no chain or cycle exists.
create table ingredient_merges (
    household_id uuid        not null references households_with_deleted (id) on delete cascade,
    from_key     text        not null,
    from_name    text        not null,
    to_key       text        not null,
    to_name      text        not null,
    created_at   timestamptz not null,

    primary key (household_id, from_key)
);

create index ingredient_merges_to on ingredient_merges (household_id, to_key);

comment on table ingredient_merges is
    'Ingredient names a household has merged into another; applied whenever one of its recipes is written.';

-- Names a household said are not what a suggestion took them for: "Butter" is
-- not a misspelt "Bitter". Kept by the folded name, so the suggestion stays
-- gone whatever the name is suggested to be merged into next.
create table ingredient_merge_dismissals (
    household_id uuid        not null references households_with_deleted (id) on delete cascade,
    name_key     text        not null,
    dismissed_at timestamptz not null,

    primary key (household_id, name_key)
);

comment on table ingredient_merge_dismissals is
    'Ingredient names a household keeps apart: never suggested for merging again.';
