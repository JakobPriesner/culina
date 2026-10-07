<script lang="ts">
  import { resolve } from '$app/paths';
  import { Image } from '$ds';
  import { m } from '$shell/i18n';

  import { imageUrl } from '../recipeImage';
  import { metaLineFor } from '../recipeMeta';
  import type { Completion } from '../types';
  import type { SearchOption } from './createSearchSession.svelte';
  import Highlighted from './Highlighted.svelte';
  import { reasonLine } from './wording';

  /**
   * The listbox the arrow keys walk through: what the half-typed word could
   * become, grouped, and then the recipes it found.
   *
   * The field keeps focus the whole time, so an option is only ever
   * highlighted (`aria-selected`), never focused.
   */
  interface Props {
    id: string;
    options: readonly SearchOption[];
    highlighted: number;
    /** What was searched for, which the matches are marked in. */
    applied: string;
    onactivate: (option: SearchOption) => void;
    onopen: (recipeId: string) => void;
  }

  let { id, options, highlighted, applied, onactivate, onopen }: Props = $props();

  const groups = $derived([
    { kind: 'recipe', label: m['search.group.recipes']() },
    { kind: 'ingredient', label: m['search.group.ingredients']() },
    { kind: 'tag', label: m['search.group.tags']() },
    { kind: 'refinement', label: m['search.group.refinements']() }
  ] as const);

  const results = $derived(options.filter((option) => option.kind === 'result'));

  function completionText(completion: Completion): string {
    return completion.kind === 'refinement'
      ? m['search.refinement']({ name: completion.label, minutes: completion.maxMinutes })
      : completion.label;
  }
</script>

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
            aria-selected={option.index === highlighted}
          >
            <button type="button" tabindex="-1" onclick={() => onactivate(option)}>
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

  {#if results.length > 0}
    <li class="heading" role="presentation">{m['search.group.results']()}</li>
    {#each results as option (option.key)}
      {#if option.kind === 'result'}
        <li
          id="{id}-{option.key}"
          class="option result"
          role="option"
          aria-selected={option.index === highlighted}
        >
          <a
            href={resolve('/(app)/recipes/[recipeId]', { recipeId: option.recipe.id })}
            tabindex="-1"
            onclick={(event) => {
              if (!event.metaKey && !event.ctrlKey) {
                event.preventDefault();
                onopen(option.recipe.id);
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
              <span class="label"><Highlighted text={option.recipe.title} query={applied} /></span>
              <span class="meta">{metaLineFor(option.recipe)}</span>
              {#if option.recipe.matchReason}
                <span class="reason">
                  <Highlighted text={reasonLine(option.recipe.matchReason)} query={applied} />
                </span>
              {/if}
            </span>
          </a>
        </li>
      {/if}
    {/each}
  {/if}
</ul>

<style>
  .list {
    margin: 0;
    padding: 0;
    list-style: none;
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
</style>
