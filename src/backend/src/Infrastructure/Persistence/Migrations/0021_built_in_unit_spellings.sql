-- Built-in units that were stored as words.
--
-- Another app that keeps units as names — Tandoor does — sent "Milliliter",
-- "Gramm" and "EL", and each was kept as a household's own counting unit: it
-- never converted for an imperial kitchen, never summed with "ml" on the
-- shopping list, and read German in an English one. Domain/Recipes/
-- UnitSpellings now reads those words as the built-ins they spell; this gives
-- the rows already written the same reading. The spellings are the ones that
-- class knew when this ran, folded the way it folds them.
create temporary table unit_spellings (spelling text primary key, code text not null) on commit drop;

insert into unit_spellings (spelling, code) values
    ('g', 'g'),
    ('kg', 'kg'),
    ('ml', 'ml'),
    ('l', 'l'),
    ('tsp', 'tsp'),
    ('tbsp', 'tbsp'),
    ('piece', 'piece'),
    ('clove', 'clove'),
    ('bunch', 'bunch'),
    ('slice', 'slice'),
    ('can', 'can'),
    ('pack', 'pack'),
    ('pinch', 'pinch'),
    ('gr', 'g'),
    ('gramm', 'g'),
    ('gram', 'g'),
    ('grams', 'g'),
    ('gramme', 'g'),
    ('kilo', 'kg'),
    ('kilos', 'kg'),
    ('kilogramm', 'kg'),
    ('kilogram', 'kg'),
    ('kilograms', 'kg'),
    ('milliliter', 'ml'),
    ('millilitre', 'ml'),
    ('milliliters', 'ml'),
    ('millilitres', 'ml'),
    ('liter', 'l'),
    ('litre', 'l'),
    ('liters', 'l'),
    ('litres', 'l'),
    ('tsps', 'tsp'),
    ('teaspoon', 'tsp'),
    ('teaspoons', 'tsp'),
    ('tl', 'tsp'),
    ('teeloeffel', 'tsp'),
    ('tbsps', 'tbsp'),
    ('tbs', 'tbsp'),
    ('tablespoon', 'tbsp'),
    ('tablespoons', 'tbsp'),
    ('el', 'tbsp'),
    ('essloeffel', 'tbsp'),
    ('stk', 'piece'),
    ('stueck', 'piece'),
    ('pieces', 'piece'),
    ('zehe', 'clove'),
    ('zehen', 'clove'),
    ('cloves', 'clove'),
    ('bund', 'bunch'),
    ('bunches', 'bunch'),
    ('scheibe', 'slice'),
    ('scheiben', 'slice'),
    ('slices', 'slice'),
    ('dose', 'can'),
    ('dosen', 'can'),
    ('cans', 'can'),
    ('packung', 'pack'),
    ('packungen', 'pack'),
    ('paeckchen', 'pack'),
    ('packs', 'pack'),
    ('packet', 'pack'),
    ('packets', 'pack'),
    ('prise', 'pinch'),
    ('prisen', 'pinch'),
    ('pinches', 'pinch');

update recipe_ingredients i
set unit = s.code
from unit_spellings s
where s.spelling = replace(replace(replace(replace(
          lower(rtrim(i.unit, '.')), 'ä', 'ae'), 'ö', 'oe'), 'ü', 'ue'), 'ß', 'ss')
  and i.unit <> s.code;

update shopping_list_items i
set unit = s.code
from unit_spellings s
where s.spelling = replace(replace(replace(replace(
          lower(rtrim(i.unit, '.')), 'ä', 'ae'), 'ö', 'oe'), 'ü', 'ue'), 'ß', 'ss')
  and i.unit <> s.code;
