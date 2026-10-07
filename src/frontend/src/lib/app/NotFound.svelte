<script lang="ts">
  import { page } from '$app/state';
  import { resolve } from '$app/paths';
  import { Stepper } from '$ds';
  import { session } from '$features/auth/session.svelte';
  import { searchOverlay } from '$features/recipes/search/overlayState.svelte';

  import { formatNumber, m } from './i18n';
  import { offersSearch } from './navigation';
  import Olli from './olli/Olli.svelte';

  /**
   * Nothing at this address, written as a recipe with real ways out; shown in place so the address
   * bar stays for spotting the typo.
   * It never says whether something exists: deleted, never existed and another household's all look
   * alike (the API answers 404 for each).
   */
  interface Props {
    kind?: 'page' | 'recipe' | 'cookbook';
    level?: 1 | 2;
  }

  let { kind = 'page', level = 2 }: Props = $props();

  const copy = {
    page: {
      title: m['notFound.page.title'],
      body: m['notFound.page.body'],
      missing: m['notFound.ing.address']
    },
    recipe: {
      title: m['notFound.recipe.title'],
      body: m['notFound.recipe.body'],
      missing: m['notFound.ing.recipe']
    },
    cookbook: {
      title: m['notFound.cookbook.title'],
      body: m['notFound.cookbook.body'],
      missing: m['notFound.ing.cookbook']
    }
  };

  const inside = $derived(session.status === 'authenticated' && session.activeHouseholdId !== null);
  // Only where the shell would open it: with a household, and not on the cooking screen.
  const searchable = $derived(inside && offersSearch(page.url.pathname));

  let servings = $state(1);

  type Ingredient = 'missing' | 'link' | 'patience';

  let mentioned = $state<Ingredient | null>(null);

  const mentions = (ingredient: Ingredient) => ({
    onpointerenter: () => (mentioned = ingredient),
    onpointerleave: () => (mentioned = null),
    onfocusin: () => (mentioned = ingredient),
    onfocusout: () => (mentioned = null)
  });
</script>

<article class="lost">
  <header class="head">
    <Olli pose="puzzled" />
    <p class="eyebrow">{m['notFound.eyebrow']()}</p>
    <svelte:element this={level === 1 ? 'h1' : 'h2'} class="title">
      {copy[kind].title()}
    </svelte:element>
    <p class="body">{copy[kind].body()}</p>

    <div class="meta">
      <Stepper
        id="lost-servings"
        bind:value={servings}
        min={1}
        max={12}
        label={m['recipe.servings.label']()}
        decreaseLabel={m['recipe.servings.fewer']()}
        increaseLabel={m['recipe.servings.more']()}
      />
      <span class="chip">{m['notFound.servings']({ count: servings })}</span>
      <span class="chip">{formatNumber(404 * servings)} kcal</span>
    </div>
  </header>

  <section class="part">
    <h3 class="label">{m['recipe.ingredients']()}</h3>
    <ul class="ingredients">
      <li class:lit={mentioned === 'missing'}>
        <span class="amount">{servings}</span>
        <span>
          {copy[kind].missing()}
          {#if kind === 'page'}<code class="address">{page.url.pathname}</code>{/if}
        </span>
      </li>
      <li class:lit={mentioned === 'link'}>
        <span class="amount">{servings}</span>
        <span>{m['notFound.ing.link']()}</span>
      </li>
      <li class:lit={mentioned === 'patience'}>
        <span class="amount">{m['notFound.ing.patience.amount']({ count: servings })}</span>
        <span>{m['notFound.ing.patience']()}</span>
      </li>
    </ul>
  </section>

  <section class="part">
    <h3 class="label">{m['recipe.steps']()}</h3>
    <ol class="steps">
      {#if kind === 'page'}
        <li {...mentions('missing')}>{m['notFound.step.typo']()}</li>
      {:else if inside}
        <li {...mentions('missing')}>
          {m['notFound.step.deleted']()}
          <a class="capsule" href={resolve('/(app)/me/household')}>{m['trash.title']()}</a>
        </li>
      {/if}

      {#if inside}
        {#if searchable}
          <li {...mentions('patience')}>
            {m['notFound.step.search']()}
            <button class="capsule" type="button" onclick={() => searchOverlay.show()}>
              {m['notFound.search']()}
            </button>
          </li>
        {/if}
        <li {...mentions('link')}>
          {m['notFound.step.library']()}
          <a class="capsule" href={resolve('/(app)')}>{m['notFound.home']()}</a>
        </li>
      {:else}
        <li {...mentions('link')}>
          {m['notFound.step.signIn']()}
          <a class="capsule" href={resolve('/(auth)/login')}>{m['auth.signIn.title']()}</a>
        </li>
      {/if}
    </ol>
  </section>
</article>

<style>
  .lost {
    display: grid;
    gap: var(--space-8);
    max-width: 34rem;
    margin-inline: auto;
    padding-block: var(--space-8) var(--space-12);
  }

  .head {
    display: grid;
    gap: var(--space-3);
  }

  .eyebrow {
    color: var(--accent);
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.12em;
    text-transform: uppercase;
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-display);
    font-weight: var(--weight-regular);
    line-height: var(--leading-tight);
    letter-spacing: -0.04em;
    text-wrap: balance;
  }

  .body {
    color: var(--text-muted);
    text-wrap: pretty;
  }

  .meta {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    margin-top: var(--space-2);
  }

  .chip {
    padding: var(--space-1) var(--space-3);
    border-radius: var(--radius-full);
    background: var(--surface-sunken);
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-variant-numeric: tabular-nums;
  }

  .part {
    display: grid;
    gap: var(--space-3);
  }

  .label {
    color: var(--text-subtle);
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.12em;
    text-transform: uppercase;
  }

  .ingredients,
  .steps {
    display: grid;
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .ingredients {
    gap: var(--space-1);
  }

  .ingredients li {
    display: grid;
    grid-template-columns: 6.5rem 1fr;
    gap: var(--space-3);
    align-items: baseline;
    padding: var(--space-2) var(--space-3);
    border-radius: var(--radius-md);
    transition: background var(--duration-base) var(--ease-out);
  }

  .ingredients li.lit {
    background: var(--surface-highlight-band);
  }

  .amount {
    font-weight: var(--weight-semibold);
    font-variant-numeric: tabular-nums;
  }

  .address {
    padding: 0 var(--space-1);
    border-radius: var(--radius-sm);
    background: var(--surface-sunken);
    color: var(--text-muted);
    font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
    font-size: var(--text-sm);
    overflow-wrap: anywhere;
  }

  .steps {
    gap: var(--space-4);
    counter-reset: step;
  }

  .steps li {
    display: grid;
    grid-template-columns: var(--space-8) 1fr;
    column-gap: var(--space-2);
    align-items: baseline;
    line-height: var(--leading-relaxed);
    counter-increment: step;
  }

  .steps li::before {
    content: counter(step);
    grid-row: span 2;
    color: var(--brand-accent);
    font-family: var(--font-editorial);
    font-size: var(--text-2xl);
    line-height: 1;
  }

  .capsule {
    grid-column: 2;
    justify-self: start;
    display: inline-flex;
    align-items: center;
    min-height: var(--control-sm);
    margin-top: var(--space-2);
    padding-inline: var(--space-4);
    border: 1px solid var(--border-accent);
    border-radius: var(--radius-full);
    background: var(--surface-accent-subtle);
    color: var(--text);
    font: inherit;
    font-weight: var(--weight-medium);
    text-decoration: none;
    cursor: pointer;
  }

  .capsule:hover {
    background: var(--surface-highlight);
  }

  @media (prefers-reduced-motion: reduce) {
    .ingredients li {
      transition: none;
    }
  }
</style>
