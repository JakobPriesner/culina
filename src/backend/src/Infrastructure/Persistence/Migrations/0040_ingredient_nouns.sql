-- A household's own counted ingredient in both numbers.
--
-- Culina swaps a noun's number by the amount ("1 Zwiebel", "2 Zwiebeln") from
-- a short curated table (Domain/Ingredients/IngredientNouns); a word it does
-- not list is never touched, so a kitchen's own "Maultasche" stays singular
-- when doubled. A household can list such a word here, and scaled recipes and
-- summed shopping lines then use its forms too.
--
-- one_key is the folded singular (ItemName.Fold), which names the row.
create table ingredient_nouns (
    household_id uuid not null references households_with_deleted (id) on delete cascade,
    one_key      text not null,
    one          text not null,
    many         text not null,

    primary key (household_id, one_key)
);

comment on table ingredient_nouns is
    'Ingredient words a household writes in both numbers, for words the built-in table does not list.';
