<script lang="ts">
  import { tick } from 'svelte';

  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { Button, Sheet } from '$ds';
  import CookbookSheet from '$features/cookbooks/CookbookSheet.svelte';
  import { formatList, m } from '$shell/i18n';

  import { createRecipeStore } from '../stores/recipes.svelte';
  import { createSearchSession } from './createSearchSession.svelte';
  import { recentSearches, rememberSearch } from './recentSearches';
  import { searchKeydown } from './searchKeyboard';
  import SearchChips from './SearchChips.svelte';
  import SearchNotice from './SearchNotice.svelte';
  import SearchRefine from './SearchRefine.svelte';
  import SearchResultsList from './SearchResultsList.svelte';
  import SearchStart from './SearchStart.svelte';
  import { createShelving } from './shelving.svelte';
  import { shelfFrom, shelvable } from './shelf';
  import { createCompletionStore } from './stores/completions.svelte';

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
    userId: string;
    onclose: () => void;
  }

  let { open, householdId, userId, onclose }: Props = $props();

  const recipes = createRecipeStore();
  const completions = createCompletionStore();
  const id = $props.id();

  let recent = $state<string[]>([]);
  let field = $state<HTMLInputElement>();

  const session = createSearchSession({
    recipes,
    completions,
    householdId: () => householdId,
    focus: () => field?.focus(),
    openRecipe: (recipeId, elsewhere) => void go(recipeId, elsewhere)
  });
  const shelving = createShelving(() => householdId);

  const interpretation = $derived(session.asking ? recipes.interpretation : null);

  /**
   * This search, as the cookbook that would ask the same question — offered
   * only when a shelf could ask any of it, and with what it could not named.
   */
  const shelf = $derived(shelfFrom(interpretation, session.tags));

  // Opened afresh each time: the recent list may have grown, and the field is
  // where the typing goes, not the close button the dialog would focus first.
  $effect(() => {
    if (open) {
      recent = recentSearches(userId);
      void tick().then(() => field?.focus());
    } else {
      session.reset();
    }
  });

  $effect(() => session.dispose);

  async function go(recipeId: string, elsewhere = false) {
    rememberSearch(userId, session.applied);

    const href = resolve('/(app)/recipes/[recipeId]', { recipeId });

    if (elsewhere) {
      window.open(href, '_blank', 'noopener');

      return;
    }

    onclose();
    await goto(href);
  }
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
        aria-expanded={session.options.length > 0}
        aria-controls="{id}-list"
        aria-activedescendant={session.highlighted >= 0
          ? `${id}-${session.options[session.highlighted]?.key}`
          : undefined}
        aria-autocomplete="list"
        autocomplete="off"
        enterkeyhint="search"
        placeholder={m['search.placeholder']()}
        value={session.typed}
        oninput={(event) => session.type(event.currentTarget.value)}
        onkeydown={(event) =>
          searchKeydown(event, {
            session,
            completions: completions.items,
            id,
            submit: () => {
              recent = rememberSearch(userId, session.typed);
              session.set(session.typed);
            }
          })}
      />
    </div>

    {#if interpretation || session.tags.length > 0}
      <div class="understood">
        <SearchChips chips={interpretation?.chips ?? []} onremove={session.remove} />
        {#if session.tags.length > 0}
          <ul class="tags">
            {#each session.tags as tag (tag.slug)}
              <li>
                <button
                  type="button"
                  class="tag"
                  aria-label={m['search.chip.remove']({ label: tag.name })}
                  onclick={() => session.removeTag(tag.slug)}
                >
                  #{tag.name} <span aria-hidden="true">×</span>
                </button>
              </li>
            {/each}
          </ul>
        {/if}
      </div>
    {/if}

    {#if session.asking && recipes.status === 'ready'}
      <SearchNotice
        {interpretation}
        total={recipes.total}
        query={session.applied}
        onastyped={session.searchAsTyped}
        onremove={session.remove}
      />
    {/if}

    <p class="count" aria-live="polite" aria-atomic="true">
      {#if session.asking && recipes.status === 'ready' && recipes.total > 0}
        {m['recipes.list.count']({ count: recipes.total })}
      {/if}
    </p>

    {#if session.asking && recipes.status === 'ready' && recipes.total > 0 && shelvable(shelf)}
      <!-- The one door from a search to a shelf. What a shelf cannot ask is
           said beside it rather than discovered on the cookbook later. -->
      <div class="shelf">
        <Button
          size="sm"
          variant="ghost"
          onclick={() =>
            shelving.offer(
              session.applied.trim() || session.tags.map((tag) => tag.name).join(', '),
              shelf.rules
            )}
        >
          {m['search.shelf.make']()}
        </Button>
        {#if shelf.behind.length > 0}
          <p class="behind">
            {m['search.shelf.behind']({
              words: formatList(shelf.behind.map((word) => m['search.shelf.word']({ word })))
            })}
          </p>
        {/if}
      </div>
    {/if}

    <SearchResultsList
      {id}
      options={session.options}
      highlighted={session.highlighted}
      applied={session.applied}
      onactivate={(option) => session.activate(option)}
      onopen={(recipeId) => void go(recipeId)}
    />

    {#if session.asking && recipes.facets}
      <SearchRefine
        facets={recipes.facets}
        typed={session.typed}
        ontag={(slug, name) => session.addTag(slug, name, false)}
        onquery={session.set}
      />
    {/if}

    {#if !session.asking && session.typed.trim().length === 0}
      <SearchStart {recent} onpick={session.set} />
    {/if}
  </div>
</Sheet>

<CookbookSheet
  open={shelving.current !== null}
  {householdId}
  preset={shelving.current}
  saving={shelving.busy}
  onsave={(name, description, rules) => void shelving.make(name, description, rules)}
  onclose={shelving.close}
/>

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

  .shelf {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
  }

  .behind {
    color: var(--text-muted);
    font-size: var(--text-xs);
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

  .tags {
    display: flex;
    gap: var(--space-2);
    margin: 0;
    padding: 0;
    list-style: none;
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
</style>
