<script lang="ts">
  import { Button, Field, TextInput } from '$ds';

  import TagChooser from '$features/recipes/filters/TagChooser.svelte';
  import { m } from '$shell/i18n';
  import { tags } from './stores/tags.svelte';
  import type { CookbookRules } from './types';

  /**
   * What a self-filling shelf asks for: three conditions that all must hold, no "any of" or nesting (that is a query language).
   * Tags are picked, since a typed slug nobody uses would match nothing; ingredients are typed, as there is no ingredient table.
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

    // Already asked for is not a second condition.
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

  // Enter in the field would otherwise submit the sheet's form and save without the typed ingredient.
  function enter(event: KeyboardEvent) {
    if (event.key === 'Enter' && event.target instanceof HTMLInputElement) {
      event.preventDefault();
      addIngredient();
    }
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
    <TagChooser
      tags={tags.items}
      selected={rules.tags}
      empty={m['cookbooks.rules.noTags']()}
      ontoggle={toggleTag}
    />
  </Field>

  <Field label={m['cookbooks.rules.ingredients']()} hint={m['cookbooks.rules.ingredientsHint']()}>
    {#snippet children({ id, describedBy })}
      <!-- svelte-ignore a11y_no_static_element_interactions -->
      <div class="entry" onkeydown={enter}>
        <TextInput
          {id}
          {describedBy}
          bind:value={typedIngredient}
          placeholder={m['cookbooks.rules.ingredientPlaceholder']()}
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

  .entry {
    display: flex;
    gap: var(--space-3);
    align-items: end;
  }

  .entry :global(> :first-child) {
    flex: 1 1 auto;
    min-width: 0;
  }

  /* The button keeps its label; otherwise the text input takes the row and squeezes "Add" onto two lines. */
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
