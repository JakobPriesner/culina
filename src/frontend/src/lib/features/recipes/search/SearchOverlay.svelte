<script lang="ts">
  import { tick } from 'svelte';

  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { Image, Sheet } from '$ds';
  import { m } from '$shell/i18n';

  import { imageUrl } from '../recipeImage';
  import { metaLineFor } from '../recipeMeta';
  import { createRecipeStore } from '../stores/recipes.svelte';
  import type { Completion, RecipeSummary, SearchChip } from '../types';
  import { recentSearches, rememberSearch } from './recentSearches';
  import SearchChips from './SearchChips.svelte';
  import SearchNotice from './SearchNotice.svelte';
  import { createCompletionStore } from './stores/completions.svelte';
  import { cuisineLabel, reasonLine, withoutChip } from './wording';

  /**
   * Search, over whatever page is on screen.
   *
   * One field, and everything else a consequence of what is in it: what the
   * server understood (chips, each removable), what it had to change to find
   * anything (said, with the way back), what the half-typed word could become,
   * and the results — in one list the arrow keys walk through, so the field
   * never loses focus and a screen reader keeps typing.
   *
   * Its own recipe store, like the picker's: searching from the plan page must
   * not empty the plan page, and closing this must leave the page exactly as
   * it was.
   */
  interface Props {
    open: boolean;
    householdId: string;
    onclose: () => void;
  }

  let { open, householdId, onclose }: Props = $props();

  const recipes = createRecipeStore();
  const completions = createCompletionStore();
  const id = $props.id();

  /** What is in the box. */
  let typed = $state('');
  /** What was last searched for, which the chips' spans refer to. */
  let applied = $state('');
  /** Tag filters picked from a completion or a refinement, by slug. */
  let tags = $state<{ slug: string; name: string }[]>([]);
  /** The query whose correction the reader turned down. */
  let asTypedFor = $state<string | null>(null);
  let highlighted = $state(-1);
  let recent = $state<string[]>([]);
  let field = $state<HTMLInputElement>();

  let suggesting: ReturnType<typeof setTimeout> | undefined;
  let searching: ReturnType<typeof setTimeout> | undefined;

  const asking = $derived(applied.trim().length > 0 || tags.length > 0);

  type Option =
    | { readonly key: string; readonly kind: 'completion'; readonly completion: Completion }
    | { readonly key: string; readonly kind: 'result'; readonly recipe: RecipeSummary };

  /** Everything the arrow keys walk through, in the order it is drawn. */
  const options = $derived<Option[]>([
    ...(typed.trim().length > 0 ? completions.items : []).map((completion, index): Option => ({
      key: `c${index}`,
      kind: 'completion',
      completion
    })),
    ...(asking ? recipes.items : []).map((recipe): Option => ({
      key: `r${recipe.id}`,
      kind: 'result',
      recipe
    }))
  ]);

  const groups = $derived([
    { kind: 'recipe', label: m['search.group.recipes']() },
    { kind: 'ingredient', label: m['search.group.ingredients']() },
    { kind: 'tag', label: m['search.group.tags']() },
    { kind: 'refinement', label: m['search.group.refinements']() }
  ] as const);

  const quick = $derived([
    m['search.quick.under30'](),
    m['search.quick.vegetarian'](),
    m['search.quick.breakfast'](),
    m['search.quick.dessert']()
  ]);

  // Opened afresh each time: the recent list may have grown, and the field is
  // where the typing goes, not the close button the dialog would focus first.
  $effect(() => {
    if (open) {
      recent = recentSearches();
      void tick().then(() => field?.focus());
    } else {
      clearTimeout(suggesting);
      clearTimeout(searching);
      typed = '';
      applied = '';
      tags = [];
      asTypedFor = null;
      completions.clear();
    }
  });

  $effect(() => () => {
    clearTimeout(suggesting);
    clearTimeout(searching);
  });

  /**
   * Two debounces, because the two answers cost different amounts: a
   * completion is a prefix over a few small tables and can keep up with the
   * word, the results are four lanes and arrive as a thought finishes.
   */
  function type(value: string) {
    typed = value;
    highlighted = -1;
    clearTimeout(suggesting);
    clearTimeout(searching);
    suggesting = setTimeout(() => void completions.complete(householdId, value), 120);
    searching = setTimeout(() => search(value), 200);
  }

  function search(value: string) {
    applied = value;

    if (value.trim().length > 0 || tags.length > 0) {
      void recipes.list(householdId, {
        query: value,
        tags: tags.map((tag) => tag.slug),
        asTyped: asTypedFor !== null && asTypedFor === value
      });
    }
  }

  /** Puts something in the box and searches it now, as if it had been typed and waited for. */
  function set(value: string) {
    typed = value;
    highlighted = -1;
    clearTimeout(suggesting);
    clearTimeout(searching);
    void completions.complete(householdId, value);
    search(value);
    field?.focus();
  }

  /** The word being typed, replaced by what it was completed to. */
  function completeWord(label: string): string {
    const words = typed.trimEnd().split(/\s+/);

    words[words.length - 1] = label;

    return `${words.join(' ')} `;
  }

  function remove(chip: SearchChip) {
    set(withoutChip(applied, chip));
  }

  function addTag(slug: string, name: string, dropWord: boolean) {
    if (!tags.some((tag) => tag.slug === slug)) {
      tags = [...tags, { slug, name }];
    }

    set(dropWord ? typed.trimEnd().split(/\s+/).slice(0, -1).join(' ') : typed);
  }

  function removeTag(slug: string) {
    tags = tags.filter((tag) => tag.slug !== slug);
    set(typed);
  }

  function hrefOf(recipeId: string): string {
    return resolve('/(app)/recipes/[recipeId]', { recipeId });
  }

  async function go(recipeId: string, elsewhere = false) {
    rememberSearch(applied);

    if (elsewhere) {
      window.open(hrefOf(recipeId), '_blank', 'noopener');

      return;
    }

    onclose();
    await goto(resolve('/(app)/recipes/[recipeId]', { recipeId }));
  }

  function activate(option: Option, elsewhere = false) {
    if (option.kind === 'result') {
      void go(option.recipe.id, elsewhere);

      return;
    }

    const completion = option.completion;

    switch (completion.kind) {
      case 'recipe':
        void go(completion.recipeId, elsewhere);
        break;
      case 'ingredient':
        set(completeWord(completion.label));
        break;
      case 'tag':
        addTag(completion.slug, completion.label, true);
        break;
      case 'refinement':
        set(
          m['search.refinement.query']({ name: completion.label, minutes: completion.maxMinutes })
        );
        break;
    }
  }

  function keydown(event: KeyboardEvent) {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();

      if (options.length > 0) {
        const step = event.key === 'ArrowDown' ? 1 : -1;
        highlighted = (highlighted + step + options.length) % options.length;
        document
          .getElementById(`${id}-${options[highlighted]!.key}`)
          ?.scrollIntoView({ block: 'nearest' });
      }
    } else if (event.key === 'Enter') {
      event.preventDefault();

      const option = options[highlighted];

      if (option) {
        activate(option, event.metaKey || event.ctrlKey);
      } else {
        recent = rememberSearch(typed);
        set(typed);
      }
    } else if (event.key === 'Escape' && typed.length > 0) {
      // Clear first, close second — the same as every other search field here.
      event.preventDefault();
      set('');
    }
  }

  function completionText(completion: Completion): string {
    return completion.kind === 'refinement'
      ? m['search.refinement']({ name: completion.label, minutes: completion.maxMinutes })
      : completion.label;
  }

  const interpretation = $derived(asking ? recipes.interpretation : null);
</script>

<Sheet {open} title={m['search.title']()} hideTitle closeLabel={m['search.close']()} {onclose}>
  <div class="search" data-fills-dialog>
    <div class="field">
      <svg
        class="icon"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="1.8"
        aria-hidden="true"
      >
        <circle cx="10.5" cy="10.5" r="6.5" />
        <path d="m15.5 15.5 4 4" stroke-linecap="round" />
      </svg>
      <input
        bind:this={field}
        type="search"
        role="combobox"
        aria-label={m['search.title']()}
        aria-expanded={options.length > 0}
        aria-controls="{id}-list"
        aria-activedescendant={highlighted >= 0 ? `${id}-${options[highlighted]?.key}` : undefined}
        aria-autocomplete="list"
        autocomplete="off"
        enterkeyhint="search"
        placeholder={m['search.placeholder']()}
        value={typed}
        oninput={(event) => type(event.currentTarget.value)}
        onkeydown={keydown}
      />
    </div>

    {#if interpretation || tags.length > 0}
      <div class="understood">
        <SearchChips chips={interpretation?.chips ?? []} onremove={remove} />
        {#if tags.length > 0}
          <ul class="tags">
            {#each tags as tag (tag.slug)}
              <li>
                <button
                  type="button"
                  class="tag"
                  aria-label={m['search.chip.remove']({ label: tag.name })}
                  onclick={() => removeTag(tag.slug)}
                >
                  #{tag.name} <span aria-hidden="true">×</span>
                </button>
              </li>
            {/each}
          </ul>
        {/if}
      </div>
    {/if}

    {#if asking && recipes.status === 'ready'}
      <SearchNotice
        {interpretation}
        total={recipes.total}
        query={applied}
        onastyped={() => {
          asTypedFor = applied;
          search(applied);
        }}
        onremove={remove}
      />
    {/if}

    <p class="count" aria-live="polite" aria-atomic="true">
      {#if asking && recipes.status === 'ready' && recipes.total > 0}
        {m['recipes.list.count']({ count: recipes.total })}
      {/if}
    </p>

    <ul class="list" id="{id}-list" role="listbox" aria-label={m['search.group.results']()}>
      {#each groups as group (group.kind)}
        {@const members = options.filter(
          (option) => option.kind === 'completion' && option.completion.kind === group.kind
        )}
        {#if members.length > 0}
          <li class="heading" role="presentation">{group.label}</li>
          {#each members as option (option.key)}
            {#if option.kind === 'completion'}
              <li
                id="{id}-{option.key}"
                class="option completion"
                role="option"
                aria-selected={options.indexOf(option) === highlighted}
              >
                <button type="button" tabindex="-1" onclick={() => activate(option)}>
                  <span class="label">{completionText(option.completion)}</span>
                  {#if option.completion.kind !== 'recipe'}
                    <span class="meta">
                      {m['recipes.list.count']({ count: option.completion.recipeCount })}
                    </span>
                  {/if}
                </button>
              </li>
            {/if}
          {/each}
        {/if}
      {/each}

      {#if asking && recipes.items.length > 0}
        <li class="heading" role="presentation">{m['search.group.results']()}</li>
        {#each options as option (option.key)}
          {#if option.kind === 'result'}
            <li
              id="{id}-{option.key}"
              class="option result"
              role="option"
              aria-selected={options.indexOf(option) === highlighted}
            >
              <a
                href={resolve('/(app)/recipes/[recipeId]', { recipeId: option.recipe.id })}
                tabindex="-1"
                onclick={(event) => {
                  if (!event.metaKey && !event.ctrlKey) {
                    event.preventDefault();
                    void go(option.recipe.id);
                  }
                }}
              >
                <span class="thumb">
                  <Image
                    src={option.recipe.imageId
                      ? imageUrl(option.recipe.id, 400, option.recipe.imageId)
                      : undefined}
                    alt=""
                    ratio={1}
                  />
                </span>
                <span class="text">
                  <span class="label">{option.recipe.title}</span>
                  <span class="meta">{metaLineFor(option.recipe)}</span>
                  {#if option.recipe.matchReason}
                    <span class="reason">{reasonLine(option.recipe.matchReason)}</span>
                  {/if}
                </span>
              </a>
            </li>
          {/if}
        {/each}
      {/if}
    </ul>

    {#if asking && recipes.facets}
      {@const facets = recipes.facets}
      <div class="refine" role="group" aria-label={m['search.refine']()}>
        <span class="refine-label">{m['search.refine']()}</span>
        {#each facets.tags as facet (facet.value)}
          <button
            type="button"
            class="facet"
            onclick={() => addTag(facet.value, facet.label ?? facet.value, false)}
          >
            {facet.label ?? facet.value} <span class="n">{facet.count}</span>
          </button>
        {/each}
        {#each facets.times as facet (facet.value)}
          <button
            type="button"
            class="facet"
            onclick={() =>
              set(`${typed.trim()} ${m['search.chip.time']({ minutes: facet.value })}`)}
          >
            {m['search.chip.time']({ minutes: facet.value })} <span class="n">{facet.count}</span>
          </button>
        {/each}
        {#each facets.cuisines as facet (facet.value)}
          <button
            type="button"
            class="facet"
            onclick={() => set(`${typed.trim()} ${cuisineLabel(facet.value)}`)}
          >
            {cuisineLabel(facet.value)} <span class="n">{facet.count}</span>
          </button>
        {/each}
      </div>
    {/if}

    {#if !asking && typed.trim().length === 0}
      <div class="start">
        {#if recent.length > 0}
          <section>
            <h3 class="heading">{m['search.recent']()}</h3>
            <ul class="plain">
              {#each recent as one (one)}
                <li>
                  <button type="button" class="again" onclick={() => set(one)}>↩ {one}</button>
                </li>
              {/each}
            </ul>
          </section>
        {/if}
        <!-- Four constant searches, not a personalised shelf: a panel whose
             contents change before anything is typed is one nobody can learn. -->
        <section>
          <h3 class="heading">{m['search.quick']()}</h3>
          <div class="quick">
            {#each quick as one (one)}
              <button type="button" class="facet" onclick={() => set(one)}>{one}</button>
            {/each}
          </div>
        </section>
      </div>
    {/if}
  </div>
</Sheet>

<style>
  .search {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    min-height: min(32rem, 70dvh);
  }

  .field {
    position: relative;
  }

  .icon {
    position: absolute;
    inset-block: 0;
    inset-inline-start: var(--space-3);
    width: var(--space-4);
    margin-block: auto;
    color: var(--text-muted);
    pointer-events: none;
  }

  input {
    width: 100%;
    min-height: var(--control-lg);
    padding-inline: calc(var(--space-8) + var(--space-3)) var(--space-3);
    border: 1px solid var(--border-strong);
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
    color: var(--text);
    font: inherit;
    font-size: var(--text-lg);
  }

  input:focus-visible {
    outline: 2px solid var(--accent);
    outline-offset: 1px;
  }

  /* One row that scrolls rather than wraps, so chips never push the results
     below the fold on a phone. A scroll container in a column shrinks to
     nothing unless told not to. */
  .understood {
    display: flex;
    flex-shrink: 0;
    gap: var(--space-2);
    overflow-x: auto;
    scrollbar-width: none;
  }

  .tags,
  .plain,
  .list {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .tags {
    display: flex;
    gap: var(--space-2);
  }

  .tag {
    min-height: var(--control-sm);
    padding: 0 var(--space-3);
    border: 1px solid var(--border);
    border-radius: var(--radius-full);
    background: var(--surface-sunken);
    color: var(--text);
    font: inherit;
    font-size: var(--text-sm);
    white-space: nowrap;
    cursor: pointer;
  }

  .count {
    min-height: 1lh;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .heading {
    padding-block: var(--space-3) var(--space-1);
    color: var(--text-muted);
    font-size: var(--text-xs);
    font-weight: var(--weight-medium);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .option > button,
  .option > a {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    width: 100%;
    min-height: 2.75rem;
    padding: var(--space-2) var(--space-3);
    border: 0;
    border-radius: var(--radius-md);
    background: none;
    color: var(--text);
    font: inherit;
    text-align: start;
    text-decoration: none;
    cursor: pointer;
  }

  .option[aria-selected='true'] > button,
  .option[aria-selected='true'] > a,
  .option > button:hover,
  .option > a:hover {
    background: var(--surface-selected);
  }

  .completion .meta {
    margin-inline-start: auto;
  }

  .thumb {
    flex: 0 0 3rem;
    width: 3rem;
    overflow: hidden;
    border-radius: var(--radius-md);
  }

  .text {
    display: flex;
    flex-direction: column;
    min-width: 0;
  }

  .label {
    font-weight: var(--weight-medium);
  }

  .meta,
  .reason {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .reason {
    font-style: italic;
  }

  .refine,
  .quick {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
  }

  .refine-label {
    color: var(--text-muted);
    font-size: var(--text-xs);
    font-weight: var(--weight-medium);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .facet,
  .again {
    min-height: var(--control-sm);
    padding: 0 var(--space-3);
    border: 1px solid var(--border);
    border-radius: var(--radius-full);
    background: var(--surface-raised);
    color: var(--text);
    font: inherit;
    font-size: var(--text-sm);
    cursor: pointer;
  }

  .again {
    border: 0;
    background: none;
    padding-inline: var(--space-1);
  }

  .n {
    color: var(--text-muted);
    font-variant-numeric: tabular-nums;
  }

  .start {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }
</style>
