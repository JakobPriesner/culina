<script lang="ts">
  import { tick } from 'svelte';
  import { resolve } from '$app/paths';
  import { Button, EmptyState, Image, SearchField } from '$ds';
  import Brand from '$shell/Brand.svelte';
  import LocalePicker from '$shell/LocalePicker.svelte';
  import ThemeToggle from '$shell/ThemeToggle.svelte';
  import { m } from '$shell/i18n';
  import { sampleRecipes, type PreviewRecipe } from './recipes';
  import RecipePreviewCard from './RecipePreviewCard.svelte';
  import RecipePreviewSurface from './RecipePreviewSurface.svelte';

  const recipes = sampleRecipes();
  let search = $state('');
  let filter = $state('all');
  let favourites = $state<string[]>(['orzo']);
  let selected = $state<PreviewRecipe | undefined>();
  let content: HTMLElement;
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
    selected = recipe;
    await tick();
    content.querySelector<HTMLElement>('h1')?.focus();
  }
</script>

<div class="preview-note">
  <span>{m['preview.notice']()}</span><a href={resolve('/design')}>{m['preview.components']()} ↗</a>
</div>
<header class="header">
  <Brand /><span class="context">{m['preview.kitchen']()}</span>
  <div class="preferences"><LocalePicker /><ThemeToggle /></div>
</header>
<main bind:this={content}>
  {#if selected}
    <RecipePreviewSurface recipe={selected} onback={() => void open()} />
  {:else}
    <div class="library">
      <div class="page-title">
        <div>
          <h1 tabindex="-1">{m['preview.title']()}</h1>
          <p class="subtitle">{m['preview.subtitle']()}</p>
        </div>
      </div>
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
          <div class="photo">
            <Image
              src={featured.image!}
              alt={featured.title}
              loading="eager"
              ratio={16 / 9}
              rounded={false}
            />
          </div>
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
              <Button variant="primary" onclick={() => void open(featured)}
                >{m['preview.open']()} <span aria-hidden="true">↗</span></Button
              >
            </div>
          </div>
        </section>
      {/if}
      <section class="collection" aria-labelledby="collection-title">
        <div class="collection-title">
          <h2 id="collection-title">{m['preview.collection']()}</h2>
          <span role="status"
            >{filtered.length === 1
              ? m['preview.countOne']()
              : m['preview.count']({ count: filtered.length })}</span
          >
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
  {/if}
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
    padding: var(--space-6) var(--space-8);
    display: flex;
    align-items: center;
    gap: var(--space-8);
  }
  .context {
    color: var(--text-muted);
    font-size: var(--text-sm);
    border-left: 1px solid var(--border);
    padding-left: var(--space-8);
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
    padding: var(--space-8) var(--space-8) var(--space-6);
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
    margin-block: var(--space-6);
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
    border-color: var(--border-strong);
    color: var(--text);
    background: var(--surface-raised);
  }
  .search {
    width: 20rem;
  }
  .feature {
    display: grid;
    grid-template-columns: 1.25fr 1fr;
    background: var(--surface-sunken);
    border-radius: var(--radius-lg);
    overflow: hidden;
  }
  .photo {
    display: grid;
    align-content: center;
  }
  .feature-copy {
    display: flex;
    flex-direction: column;
    justify-content: center;
    gap: var(--space-4);
    padding: var(--space-8) var(--space-12);
  }
  .feature-copy h2 {
    font-family: var(--font-editorial);
    font-weight: var(--weight-regular);
    font-size: var(--text-3xl);
    letter-spacing: -0.035em;
  }
  .feature-copy > p:not(.eyebrow) {
    color: var(--text-muted);
    max-width: 34ch;
  }
  .meta {
    font-size: var(--text-sm);
    color: var(--text-muted);
    padding-block: var(--space-2);
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
    font-size: var(--text-xl);
  }
  .collection-title span,
  .footer {
    font-size: var(--text-xs);
    color: var(--text-muted);
  }
  .grid {
    display: grid;
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: var(--space-8);
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
  @media (max-width: 47.999rem) {
    .header {
      padding: var(--space-4) var(--space-6);
      gap: var(--space-2);
      flex-wrap: wrap;
    }
    .preferences {
      gap: var(--space-1);
    }
    .library {
      padding: var(--space-6);
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
    .feature-copy {
      padding: var(--space-6);
    }
    .grid {
      grid-template-columns: 1fr;
      gap: var(--space-3);
    }
    .preview-note {
      gap: var(--space-1);
      font-size: var(--text-xs);
    }
  }
</style>
