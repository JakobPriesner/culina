<script lang="ts">
  import { Button, Checkbox, Field, TextInput } from '$ds';

  import { m } from '$shell/i18n';
  import { tags } from './stores/tags.svelte';
  import type { CookbookRules } from './types';

  /**
   * What a shelf that fills itself asks for.
   *
   * Three questions, all of which must hold. There is no "any of these" and no
   * nesting: the shelves worth having are the narrow ones, and a rule builder
   * with brackets in it is a query language somebody has to learn.
   *
   * Tags are picked rather than typed, because a rule naming a slug nobody uses
   * would quietly match nothing and look broken. Ingredients are typed, because
   * there is no ingredient table to pick from — an ingredient is whatever
   * somebody wrote in a recipe.
   */
  interface Props {
    householdId: string;
    rules: CookbookRules;
    onchange: (rules: CookbookRules) => void;
  }

  let { householdId, rules, onchange }: Props = $props();

  let typedIngredient = $state('');

  $effect(() => {
    void tags.load(householdId);
  });

  const minutes = $derived(rules.maxMinutes === null ? '' : String(rules.maxMinutes));

  function toggleTag(slug: string, on: boolean) {
    onchange({
      ...rules,
      tags: on ? [...rules.tags, slug] : rules.tags.filter((tag) => tag !== slug)
    });
  }

  function addIngredient() {
    const name = typedIngredient.trim();

    // Already asked for is not a second condition, and adding it twice would
    // make the shelf look like it wants two of something.
    if (
      name.length === 0 ||
      rules.ingredients.some((one) => one.toLowerCase() === name.toLowerCase())
    ) {
      typedIngredient = '';

      return;
    }

    onchange({ ...rules, ingredients: [...rules.ingredients, name] });
    typedIngredient = '';
  }

  function removeIngredient(name: string) {
    onchange({ ...rules, ingredients: rules.ingredients.filter((one) => one !== name) });
  }

  function setMinutes(value: string) {
    const parsed = Number.parseInt(value, 10);

    onchange({
      ...rules,
      maxMinutes: value.trim().length === 0 || Number.isNaN(parsed) || parsed <= 0 ? null : parsed
    });
  }
</script>

<section class="rules">
  <h3 class="heading">{m['cookbooks.rules.title']()}</h3>
  <p class="hint">{m['cookbooks.rules.hint']()}</p>

  <Field label={m['cookbooks.rules.tags']()} hint={m['cookbooks.rules.tagsHint']()} group>
    {#if tags.items.length === 0}
      <p class="empty">{m['cookbooks.rules.noTags']()}</p>
    {:else}
      <ul class="tags">
        {#each tags.items as tag (tag.slug)}
          <li>
            <Checkbox
              label="{tag.name} · {m['cookbooks.rules.usedBy']({ count: tag.recipeCount })}"
              checked={rules.tags.includes(tag.slug)}
              onchange={(on) => toggleTag(tag.slug, on)}
            />
          </li>
        {/each}
      </ul>
    {/if}
  </Field>

  <Field label={m['cookbooks.rules.ingredients']()} hint={m['cookbooks.rules.ingredientsHint']()}>
    {#snippet children({ id, describedBy })}
      <div class="entry">
        <TextInput
          {id}
          {describedBy}
          bind:value={typedIngredient}
          placeholder="Hähnchen"
          maxlength={120}
        />
        <Button onclick={addIngredient}>{m['cookbooks.rules.ingredientAdd']()}</Button>
      </div>
    {/snippet}
  </Field>

  {#if rules.ingredients.length > 0}
    <ul class="chosen">
      {#each rules.ingredients as ingredient (ingredient)}
        <li>
          <button
            type="button"
            onclick={() => removeIngredient(ingredient)}
            aria-label={m['cookbooks.rules.ingredientRemove']({ name: ingredient })}
          >
            {ingredient}
            <span aria-hidden="true">×</span>
          </button>
        </li>
      {/each}
    </ul>
  {/if}

  <Field label={m['cookbooks.rules.maxMinutes']()} hint={m['cookbooks.rules.maxMinutesHint']()}>
    {#snippet children({ id, describedBy })}
      <TextInput
        {id}
        {describedBy}
        value={minutes}
        inputmode="numeric"
        maxlength={5}
        oninput={setMinutes}
      />
    {/snippet}
  </Field>
</section>

<style>
  .rules {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    padding-top: var(--space-4);
    border-top: 1px solid var(--border);
  }

  .heading {
    font-size: var(--text-base);
    font-weight: var(--weight-medium);
  }

  .hint {
    color: var(--text-muted);
    font-size: var(--text-sm);
    max-width: 44ch;
  }

  .empty {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .tags {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    margin: 0;
    padding: 0;
    list-style: none;
    /* Long enough to be worth scrolling past rather than pushing the rest of
       the form off the sheet. */
    max-height: 14rem;
    overflow-y: auto;
    /* Gutter for a bar that takes width, padding for an overlay bar that does
       not and is painted over the rules instead. See RecipePicker for why both
       are needed. */
    scrollbar-gutter: stable;
    padding-inline-end: var(--space-2);
    /* A bounded list inside a sheet that also scrolls: without this, reaching
       the last rule carries on and scrolls the sheet behind it. */
    overscroll-behavior: contain;
  }

  .entry {
    display: flex;
    gap: var(--space-3);
    align-items: end;
  }

  .entry :global(> :first-child) {
    flex: 1 1 auto;
    min-width: 0;
  }

  /* The button keeps its label. Without this the text input takes the row and
     squeezes "Add" onto two lines of one letter each. */
  .entry :global(> :last-child) {
    flex: 0 0 auto;
  }

  .chosen {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .chosen button {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
    min-height: var(--control-sm);
    padding: 0 var(--space-3);
    border: 1px solid var(--border);
    border-radius: var(--radius-full);
    background: var(--surface-sunken);
    color: var(--text);
    font: inherit;
    font-size: var(--text-sm);
    cursor: pointer;
  }

  .chosen button:hover {
    border-color: var(--border-strong);
  }
</style>
