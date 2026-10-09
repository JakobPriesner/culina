<script lang="ts">
  import { Field, Select, TextArea, TextInput } from '$ds';
  import {
    minutesShown,
    minutesWrong,
    totalMinutes,
    yieldShown,
    yieldWrong,
    type MinutesField,
    type TypedNumbers
  } from '$features/recipes/editor/numbers';
  import type { Recipe, RecipeLanguage } from '$features/recipes/types';
  import { yieldNoun } from '$features/recipes/yieldWords';
  import { m } from '$shell/i18n';

  interface Props {
    recipe: Recipe;
    typed: TypedNumbers;
    onchange: (patch: Partial<Recipe>) => void;
    onyield: (text: string) => void;
    onminutes: (which: MinutesField, text: string) => void;
  }

  let { recipe, typed, onchange, onyield, onminutes }: Props = $props();

  const yieldText = $derived(yieldShown(typed, recipe.yieldAmount ?? 1));
  const prepText = $derived(minutesShown(typed, 'prepMinutes', recipe.prepMinutes));
  const cookText = $derived(minutesShown(typed, 'cookMinutes', recipe.cookMinutes));
  const total = $derived(totalMinutes(recipe.prepMinutes, recipe.cookMinutes));

  const languages = $derived([
    { value: 'en', label: m['locale.en']() },
    { value: 'de', label: m['locale.de']() }
  ]);
</script>

<div class="fields">
  <Field label={m['editor.title']()}>
    {#snippet children({ id, describedBy, invalid })}
      <TextInput
        {id}
        {describedBy}
        {invalid}
        size="display"
        value={recipe.title}
        oninput={(value) => onchange({ title: value })}
      />
    {/snippet}
  </Field>

  <Field label={m['editor.description']()} optionalText={m['editor.optional']()}>
    {#snippet children({ id, describedBy, invalid })}
      <TextArea
        {id}
        {describedBy}
        {invalid}
        maxlength={2000}
        value={recipe.description ?? ''}
        oninput={(value) => onchange({ description: value || null })}
      />
    {/snippet}
  </Field>

  <!-- Not guessed: the search index picks its stemmer from this, and ingredient suggestions their language. -->
  <Field label={m['editor.language']()} hint={m['editor.languageHint']()}>
    {#snippet children({ id, describedBy, invalid })}
      <Select
        {id}
        {describedBy}
        {invalid}
        inline
        options={languages}
        value={recipe.language}
        onchange={(value) => onchange({ language: value as RecipeLanguage })}
      />
    {/snippet}
  </Field>

  <div class="meta">
    <fieldset class="group">
      <legend class="legend">{m['editor.yieldGroup']()}</legend>

      <div class="pair yield">
        <Field
          label={m['editor.yieldAmount']()}
          error={yieldWrong(yieldText) ? m['editor.yieldWrong']() : undefined}
        >
          {#snippet children({ id, describedBy, invalid })}
            <TextInput
              {id}
              {describedBy}
              {invalid}
              inputmode="numeric"
              value={yieldText}
              oninput={onyield}
            />
          {/snippet}
        </Field>

        <Field label={m['editor.yieldLabel']()} optionalText={m['editor.optional']()}>
          {#snippet children({ id, describedBy, invalid })}
            <TextInput
              {id}
              {describedBy}
              {invalid}
              maxlength={40}
              placeholder={yieldNoun({ yieldKind: recipe.yieldKind, yieldLabel: null })}
              value={recipe.yieldLabel ?? ''}
              oninput={(value) => onchange({ yieldLabel: value.trim() ? value : null })}
            />
          {/snippet}
        </Field>
      </div>

      <p class="note">{m['editor.yieldLabelHint']()}</p>
    </fieldset>

    <fieldset class="group">
      <legend class="legend">{m['editor.timeGroup']()}</legend>

      <div class="pair">
        <Field
          label={m['editor.prepMinutes']()}
          error={minutesWrong(prepText) ? m['editor.minutesWrong']() : undefined}
        >
          {#snippet children({ id, describedBy, invalid })}
            <TextInput
              {id}
              {describedBy}
              {invalid}
              inputmode="numeric"
              value={prepText}
              oninput={(value) => onminutes('prepMinutes', value)}
            />
          {/snippet}
        </Field>

        <Field
          label={m['editor.cookMinutes']()}
          error={minutesWrong(cookText) ? m['editor.minutesWrong']() : undefined}
        >
          {#snippet children({ id, describedBy, invalid })}
            <TextInput
              {id}
              {describedBy}
              {invalid}
              inputmode="numeric"
              value={cookText}
              oninput={(value) => onminutes('cookMinutes', value)}
            />
          {/snippet}
        </Field>
      </div>

      <p class="note total" class:said={total !== null}>
        {total === null ? m['editor.minutesOptional']() : m['editor.totalTime']({ count: total })}
      </p>
    </fieldset>
  </div>
</div>

<style>
  .fields {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    min-width: 0;
  }

  .meta {
    display: grid;
    gap: var(--space-6);
    min-width: 0;
    margin-top: var(--space-2);
  }

  .group {
    container-type: inline-size;
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    min-width: 0;
    margin: 0;
    padding: 0;
    border: none;
  }

  /* Caption for a pair of fields; the size between section heading and field label. */
  .legend {
    padding: 0;
    color: var(--text-subtle);
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .pair {
    display: grid;
    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
    gap: var(--space-3);
    align-items: start;
    min-width: 0;
  }

  /* A count is short and a noun a word: equal columns made "4" as wide as "Portionen". */
  .yield {
    grid-template-columns: 6rem minmax(0, 1fr);
  }

  /* Keeps both fields usable when enlarged text leaves little room; the threshold follows text size. */
  @container (max-width: 16rem) {
    .pair {
      grid-template-columns: minmax(0, 1fr);
    }
  }

  .note {
    color: var(--text-subtle);
    font-size: var(--text-xs);
    line-height: var(--leading-normal);
  }

  .total {
    transition: color var(--duration-base) var(--ease-out);
  }

  .total.said {
    color: var(--text-muted);
    font-variant-numeric: tabular-nums;
  }

  @media (min-width: 64rem) {
    .meta {
      grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
    }
  }
</style>
