<script lang="ts">
  import { Button, Field, FilterChip, TextInput, VisuallyHidden } from '$ds';
  import type { TagInUse } from '$features/cookbooks/stores/tags.svelte';
  import TagChooser from '$features/recipes/filters/TagChooser.svelte';
  import type { TagSuggestion } from '$features/recipes/stores/tagSuggestions.svelte';
  import { m } from '$shell/i18n';

  /**
   * A recipe's tags, and the ones it could carry.
   *
   * The kitchen's own tags are chosen with the control that chooses them
   * everywhere else — the library's filters and a cookbook's rules — because a
   * tag is the same thing in all three places. A new one is typed. And beneath
   * both, what the recipe could be tagged with and is not: offered, never
   * applied, in the kitchen's own words wherever it has them.
   *
   * A recipe's tags arrive as slugs and leave as whatever was added — the
   * server makes a tag out of a name the first time it is saved. So a tag here
   * is the kitchen's when its slug or its name says so, and new otherwise.
   */
  interface Props {
    /** What the recipe carries: slugs, and the names of tags added since. */
    tags: readonly string[];
    /** Every tag the kitchen uses. */
    household: readonly TagInUse[];
    suggestions: readonly TagSuggestion[];
    onchange: (tags: string[]) => void;
  }

  let { tags, household, suggestions, onchange }: Props = $props();

  const id = $props.id();

  let typed = $state('');

  /** Case and accents ignored, so "creme" and "Crème" are one tag. */
  const fold = (text: string) =>
    text.trim().toLocaleLowerCase().normalize('NFD').replace(/\p{M}/gu, '');

  const isTag = (entry: string, tag: TagInUse) =>
    entry === tag.slug || fold(entry) === fold(tag.name);

  const chosen = $derived(
    household.filter((tag) => tags.some((entry) => isTag(entry, tag))).map((tag) => tag.slug)
  );

  /** Added here and not yet one of the kitchen's, so the chooser cannot show them. */
  const added = $derived(tags.filter((entry) => !household.some((tag) => isTag(entry, tag))));

  const carries = (name: string) =>
    tags.some((entry) => fold(entry) === fold(name)) ||
    household.some((tag) => fold(tag.name) === fold(name) && chosen.includes(tag.slug));

  const offered = $derived(
    suggestions.filter(
      (one) => !carries(one.name) && !(one.slug !== null && chosen.includes(one.slug))
    )
  );

  function add(entry: string) {
    const name = entry.trim();

    if (name.length === 0 || carries(name)) {
      typed = '';

      return;
    }

    // A word the kitchen already has goes in as that tag, so "Italienisch"
    // typed here is its "italienisch" rather than a second spelling of it.
    const known = household.find((tag) => fold(tag.name) === fold(name));

    onchange([...tags, known?.slug ?? name]);
    typed = '';
  }

  /** Enter adds, as it does in every list in this editor. */
  function enter(event: KeyboardEvent) {
    if (event.key === 'Enter' && event.target instanceof HTMLInputElement) {
      event.preventDefault();
      add(typed);
    }
  }

  function toggle(slug: string, on: boolean) {
    const tag = household.find((one) => one.slug === slug);

    if (!tag) {
      return;
    }

    onchange(on ? [...tags, slug] : tags.filter((entry) => !isTag(entry, tag)));
  }
</script>

<div class="tags">
  <TagChooser
    tags={household}
    selected={chosen}
    ontoggle={toggle}
    empty={m['editor.tags.none']()}
  />

  {#if added.length > 0}
    <ul class="added" aria-label={m['editor.tags.added']()}>
      {#each added as entry (entry)}
        <li>
          <FilterChip selected onclick={() => onchange(tags.filter((one) => one !== entry))}>
            <span>{entry}</span>
            <span class="remove" aria-hidden="true">×</span>
            <VisuallyHidden>{m['editor.tags.remove']({ name: entry })}</VisuallyHidden>
          </FilterChip>
        </li>
      {/each}
    </ul>
  {/if}

  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div class="row" onkeydown={enter}>
    <Field label={m['editor.tags.new']()}>
      {#snippet children({ id: field, describedBy })}
        <TextInput id={field} {describedBy} bind:value={typed} autocomplete="off" />
      {/snippet}
    </Field>
    <Button onclick={() => add(typed)} disabled={typed.trim().length === 0}>
      {m['editor.tags.add']()}
    </Button>
  </div>

  {#if offered.length > 0}
    <div class="suggested">
      <p class="lead" id="{id}-suggested">{m['editor.tags.suggested']()}</p>
      <ul class="offers" aria-labelledby="{id}-suggested">
        {#each offered as one (one.name)}
          <li>
            <FilterChip selected={false} onclick={() => add(one.slug ?? one.name)}>
              + {one.name}
            </FilterChip>
          </li>
        {/each}
      </ul>
    </div>
  {/if}
</div>

<style>
  .tags {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
  }

  .added,
  .offers {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .remove {
    color: var(--text-muted);
  }

  .lead {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  /* The button sits on the field's baseline, beside it rather than under it. */
  .row {
    display: flex;
    gap: var(--space-2);
    align-items: flex-end;
  }

  .row > :global(:first-child) {
    flex: 1;
    min-width: 0;
  }

  .suggested {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }
</style>
