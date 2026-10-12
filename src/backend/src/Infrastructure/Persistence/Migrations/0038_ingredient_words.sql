-- What a wanted ingredient asks of a recipe's ingredient names, word by word.
--
-- Filtering by an ingredient (the search's ingredient chips, its exclusions, a
-- smart cookbook's rules, the suggestions' "with what I have") used to ask
-- that the whole wanted text appear in a name: "grüner Spargel" found
-- "grüner Spargel" and nothing else, not "Spargel, grün", "grünen Spargel" or
-- "Grünspargel"; "Tomaten" did not find "Tomate". Now every word of it has to
-- appear somewhere in the name, in either German fold, with a plural or
-- declension ending taken off first. A name is still matched inside a
-- compound as before ("Spargel" finds "Grünspargel"), and a broader wanted
-- ingredient still finds the narrower ones ("Kokosmilch" finds
-- "Kokosmilch light"), but not the other way round.
--
-- The ending comes off only a word long enough to keep a stem of four
-- letters or more: "Reis" must not become "rei", which is in "Rührei".

create function culina_ingredient_stem(word text) returns text
    language sql
    immutable
    strict
    parallel safe
    return case
        when length(word) >= 6 and word ~ '(er|es|en|em)$' then left(word, -2)
        when length(word) >= 5 and word ~ '[ens]$' then left(word, -1)
        else word
    end;

comment on function culina_ingredient_stem(text) is
    'A folded word without its plural or declension ending: gruener -> gruen, tomaten -> tomat.';

-- The two folds split a text into the same words (only umlauts differ), so
-- they are paired by position.
create function culina_wanted_words(wanted text) returns table (ae text, a text)
language sql immutable strict parallel safe
rows 3
as $$
    select culina_ingredient_stem(w.ae), culina_ingredient_stem(w.a)
    from unnest(
        string_to_array(culina_fold_ae(wanted), ' '),
        string_to_array(culina_fold_a(wanted), ' ')) as w (ae, a)
    where w.ae <> '';
$$;

comment on function culina_wanted_words(text) is
    'The stemmed words of a wanted ingredient in both folds; each must appear in a name for the name to hold it.';
