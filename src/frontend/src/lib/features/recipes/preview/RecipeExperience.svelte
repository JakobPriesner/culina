<script lang="ts">
  import { tick } from 'svelte';
  import { resolve } from '$app/paths';
  import { Button, EmptyState, Image, SearchField } from '$ds';
  import NavIcon from '$shell/NavIcon.svelte';
  import Brand from '$shell/Brand.svelte';
  import LocalePicker from '$shell/LocalePicker.svelte';
  import ThemeToggle from '$shell/ThemeToggle.svelte';
  import { m } from '$shell/i18n';
  import { sampleRecipes, type PreviewRecipe, type PreviewProgress } from './recipes';
  import RecipePreviewCard from './RecipePreviewCard.svelte';
  import RecipePreviewSurface from './RecipePreviewSurface.svelte';

  const recipes = sampleRecipes();
  let search = $state('');
  let filter = $state('all');
  let favourites = $state<string[]>(['orzo']);
  let selected = $state<PreviewRecipe | undefined>();
  let detail = $state<HTMLDivElement>();
  let library: HTMLDivElement;
  let opener: HTMLElement | undefined;
  let libraryScroll = 0;
  let progress = $state<Record<string, PreviewProgress>>({});
  const activeRecipes = $derived(recipes.filter((recipe) => progress[recipe.id]?.cooking));
  const filters = [
    { id: 'all', label: m['preview.all']() },
    { id: 'favourites', label: m['preview.favourites']() },
    { id: 'quick', label: m['preview.quick']() }
  ];
  const filtered = $derived(
    recipes.filter(
      (recipe) =>
        `${recipe.title} ${recipe.tag} ${recipe.ingredients.map((i) => i.name).join(' ')}`
          .toLocaleLowerCase()
          .includes(search.trim().toLocaleLowerCase()) &&
        (filter !== 'favourites' || favourites.includes(recipe.id)) &&
        (filter !== 'quick' || recipe.minutes <= 30)
    )
  );
  const featured = recipes[0]!;
  function toggleFavourite(id: string) {
    favourites = favourites.includes(id)
      ? favourites.filter((value) => value !== id)
      : [...favourites, id];
  }
  async function open(recipe?: PreviewRecipe) {
    if (recipe) {
      opener = document.activeElement instanceof HTMLElement ? document.activeElement : undefined;
      libraryScroll = window.scrollY;
      progress[recipe.id] ??= { servings: 2, currentStep: 0, cooking: false, checked: {} };
      selected = recipe;
      await tick();
      detail
        ?.querySelector<HTMLElement>(progress[recipe.id]?.cooking ? '[aria-current="step"]' : 'h1')
        ?.focus();
    } else {
      selected = undefined;
      await tick();
      window.scrollTo({ top: libraryScroll, behavior: 'instant' });
      (opener?.isConnected ? opener : library.querySelector<HTMLElement>('h1'))?.focus({
        preventScroll: true
      });
    }
  }
</script>

<div class="preview-note">
  <span>{m['preview.notice']()}</span><a href={resolve('/design')}>{m['preview.components']()} ↗</a>
</div>
<header class="header">
  <Brand /><span class="context">{m['preview.footer']()}</span>
  <div class="preferences"><LocalePicker compact /><ThemeToggle /></div>
</header>
<main>
  {#if selected}
    <div bind:this={detail}>
      <RecipePreviewSurface
        recipe={selected}
        progress={progress[selected.id]!}
        onprogress={(value) => {
          if (selected) progress[selected.id] = value;
        }}
        onback={() => void open()}
      />
    </div>
  {/if}
  <div class="library" bind:this={library} hidden={selected !== undefined}>
    <div class="page-title">
      <div>
        <p class="kitchen-label">
          <span aria-hidden="true"><NavIcon icon="recipes" current={false} /></span>{m[
            'preview.kitchen'
          ]()}
        </p>
        <h1 tabindex="-1">{m['preview.title']()}</h1>
        <p class="subtitle">{m['preview.subtitle']()}</p>
      </div>
    </div>
    {#each activeRecipes as recipe (recipe.id)}
      <div class="resume">
        <div>
          <span>{m['preview.inProgress']()}</span>
          <p>{recipe.title}</p>
        </div>
        <Button size="sm" onclick={() => void open(recipe)}
          >{m['preview.resume']()} <span aria-hidden="true">→</span></Button
        >
      </div>
    {/each}
    <div class="tools">
      <div class="filters" role="group" aria-label={m['preview.filters']()}>
        {#each filters as item (item.id)}<button
            class:active={filter === item.id}
            aria-pressed={filter === item.id}
            onclick={() => (filter = item.id)}>{item.label}</button
          >{/each}
      </div>
      <div class="search">
        <SearchField
          id="preview-search"
          label={m['preview.search']()}
          placeholder={m['preview.search']()}
          clearLabel={m['preview.clear']()}
          bind:value={search}
        />
      </div>
    </div>
    {#if !search && filter === 'all'}
      <section class="feature" aria-labelledby="featured-title">
        <div class="feature-copy">
          <p class="eyebrow">{m['preview.featured']()}</p>
          <h2 id="featured-title">{featured.title}</h2>
          <p>{featured.description}</p>
          <span class="meta"
            >{m['preview.minutes']({ count: featured.minutes })} · {m[
              'preview.twoServings'
            ]()}</span
          >
          <div>
            <Button variant="secondary" onclick={() => void open(featured)}
              >{m['preview.open']()} <span aria-hidden="true">→</span></Button
            >
          </div>
        </div>
        <div class="photo">
          <Image src={featured.image!} alt={featured.title} loading="eager" fill rounded={false} />
        </div>
      </section>
    {/if}
    <section class="collection" aria-labelledby="collection-title">
      <div class="collection-title">
        <h2 id="collection-title">{m['preview.collection']()}</h2>
        <span role="status">{m['preview.count']({ count: filtered.length })}</span>
      </div>
      {#if filtered.length}
        <div class="grid">
          {#each filtered as recipe (recipe.id)}<RecipePreviewCard
              {recipe}
              favourite={favourites.includes(recipe.id)}
              onopen={() => void open(recipe)}
              onfavourite={() => toggleFavourite(recipe.id)}
            />{/each}
        </div>
      {:else}
        <EmptyState title={m['preview.empty.title']()} body={m['preview.empty.body']()}
          >{#snippet action()}<Button
              onclick={() => {
                search = '';
                filter = 'all';
              }}>{m['preview.reset']()}</Button
            >{/snippet}</EmptyState
        >
      {/if}
    </section>
    <footer class="footer">{m['preview.footer']()}</footer>
  </div>
</main>

<style>
  .preview-note {
    display: flex;
    justify-content: center;
    flex-wrap: wrap;
    gap: var(--space-4);
    padding: var(--space-2) var(--space-4);
    background: var(--surface-sunken);
    color: var(--text-muted);
    font-size: var(--text-xs);
  }
  .preview-note a {
    color: var(--text-muted);
  }
  .header {
    max-width: var(--layout-wide);
    margin-inline: auto;
    min-height: 5.5rem;
    padding: var(--space-4) var(--layout-gutter-end) var(--space-4) var(--layout-gutter-start);
    border-bottom: 1px solid var(--border);
    display: flex;
    align-items: center;
    gap: var(--space-8);
  }
  .context {
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-family: var(--font-editorial);
    font-style: italic;
  }
  .preferences {
    margin-left: auto;
    display: flex;
    align-items: center;
    gap: var(--space-4);
  }
  .library {
    max-width: var(--layout-wide);
    margin-inline: auto;
    padding: var(--layout-page-space) var(--layout-gutter-end) var(--layout-page-space)
      var(--layout-gutter-start);
  }
  .kitchen-label {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    color: var(--accent);
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.12em;
    text-transform: uppercase;
  }
  .kitchen-label span {
    width: var(--space-4);
    height: var(--space-4);
  }
  .eyebrow {
    font-size: var(--text-xs);
    letter-spacing: 0.12em;
    text-transform: uppercase;
    color: var(--text-muted);
  }
  h1 {
    font-family: var(--font-editorial);
    font-size: var(--text-display);
    font-weight: var(--weight-regular);
    letter-spacing: -0.04em;
    margin-top: var(--space-3);
  }
  .subtitle {
    color: var(--text-muted);
    margin-top: var(--space-3);
  }
  .tools {
    margin-block: var(--space-8) var(--space-6);
    display: flex;
    flex-wrap: wrap;
    justify-content: space-between;
    align-items: center;
    gap: var(--space-4);
  }
  .filters {
    display: flex;
    gap: var(--space-2);
    flex-wrap: wrap;
  }
  .filters button {
    min-height: var(--control-sm);
    padding-inline: var(--space-4);
    border: 1px solid transparent;
    border-radius: var(--radius-full);
    background: transparent;
    color: var(--text-muted);
    font: inherit;
    font-size: var(--text-sm);
    cursor: pointer;
  }
  .filters button:hover {
    background: var(--surface-hover);
    color: var(--text);
  }
  .filters button.active {
    border-color: var(--accent);
    color: var(--accent-contrast);
    background: var(--accent);
  }
  .search {
    width: min(22rem, 100%);
  }
  .feature {
    --border-focus: var(--text-on-feature);
    display: grid;
    grid-template-columns: minmax(0, 0.9fr) minmax(0, 1.1fr);
    background: var(--surface-feature);
    color: var(--text-on-feature);
    border-radius: var(--radius-lg);
    overflow: hidden;
  }
  .photo {
    min-height: 22rem;
  }
  .library[hidden] {
    display: none;
  }
  .resume {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-4);
    margin-top: var(--space-6);
    padding-block: var(--space-4);
    border-block: 1px solid var(--border);
  }
  .resume span {
    font-size: var(--text-xs);
    color: var(--text-muted);
  }
  .resume p {
    margin-top: var(--space-1);
    font-weight: var(--weight-medium);
  }
  .feature-copy {
    display: flex;
    flex-direction: column;
    justify-content: center;
    gap: var(--space-4);
    padding: var(--space-8) var(--space-12);
    align-items: flex-start;
  }
  .feature-copy h2 {
    font-family: var(--font-editorial);
    font-weight: var(--weight-regular);
    font-size: clamp(1.75rem, 2.8vw, 2.5rem);
    line-height: 1.18;
    letter-spacing: -0.035em;
  }
  .feature-copy > p:not(.eyebrow) {
    color: var(--text-on-feature);
    font-size: var(--text-sm);
    line-height: var(--leading-relaxed);
    max-width: 38ch;
  }
  .feature .eyebrow {
    color: var(--text-on-feature);
  }
  .meta {
    font-size: var(--text-sm);
    color: var(--text-on-feature);
    padding-block: var(--space-1);
  }
  .collection {
    margin-top: var(--space-12);
  }
  .collection-title {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    margin-bottom: var(--space-6);
    gap: var(--space-4);
  }
  .collection-title h2 {
    font-family: var(--font-editorial);
    font-weight: var(--weight-regular);
    font-size: var(--text-2xl);
    letter-spacing: -0.025em;
  }
  .collection-title span,
  .footer {
    font-size: var(--text-xs);
    color: var(--text-muted);
  }
  .grid {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: var(--space-6);
  }
  .footer {
    margin-top: var(--space-8);
    padding-block: var(--space-4);
    border-top: 1px solid var(--border);
  }
  @media (max-width: 63.999rem) {
    .feature-copy {
      padding: var(--space-6);
    }
    .context {
      display: none;
    }
  }
  @media (width < 64rem) {
    .header {
      padding: var(--space-3) var(--layout-gutter-end) var(--space-3) var(--layout-gutter-start);
      gap: var(--space-2);
      flex-wrap: wrap;
    }
    .preferences {
      gap: var(--space-1);
    }
    .library {
      padding: var(--space-6) var(--layout-gutter-end) var(--space-6) var(--layout-gutter-start);
    }
    .search {
      width: 100%;
    }
    .tools {
      flex-direction: column-reverse;
      align-items: stretch;
    }
    .filters button {
      padding-inline: var(--space-3);
    }
    .feature {
      grid-template-columns: 1fr;
    }
    .feature .photo {
      grid-row: 1;
    }
    .feature-copy {
      padding: var(--space-6);
    }
    .photo {
      min-height: 0;
      aspect-ratio: 16 / 10;
    }
    .resume {
      align-items: flex-start;
      flex-direction: column;
    }
    .preview-note {
      gap: var(--space-1);
      font-size: var(--text-xs);
    }
  }
  @media (width < 40rem) {
    .grid {
      grid-template-columns: minmax(0, 1fr);
    }
  }
  @media (min-width: 64rem) {
    .grid {
      grid-template-columns: repeat(3, minmax(0, 1fr));
    }
  }
</style>
