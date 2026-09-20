<script lang="ts">
  import { Skeleton } from '$ds';
  import { m } from '$shell/i18n';

  interface Props {
    /** Whether to reserve space for a hero photo (default true). */
    hasPhoto?: boolean;
  }

  let { hasPhoto = true }: Props = $props();
</script>

<article class="surface" aria-busy="true" aria-label={m['recipes.list.loading']()}>
  {#if hasPhoto}
    <div class="hero">
      <Skeleton shape="block" />
    </div>
  {/if}

  <header class="head">
    <div class="titleRow">
      <div class="title">
        <Skeleton width="65%" height="2.5rem" />
      </div>

      <div class="actions">
        <Skeleton shape="block" width="8.5rem" height="var(--control-md)" />
        <Skeleton shape="circle" width="var(--control-md)" height="var(--control-md)" />
      </div>
    </div>

    <div class="meta">
      <Skeleton width="35%" height="1rem" />
    </div>

    <div class="description">
      <Skeleton width="90%" height="1rem" />
      <Skeleton width="75%" height="1rem" />
    </div>
  </header>

  <div class="servings">
    <Skeleton shape="block" width="8.5rem" height="var(--control-sm)" />
    <Skeleton shape="block" width="6.5rem" height="var(--control-sm)" />
  </div>

  <div class="body">
    <section class="ingredients" aria-hidden="true">
      <div class="section-head">
        <Skeleton width="6rem" height="1.5rem" />
        <Skeleton shape="block" width="7.5rem" height="var(--control-sm)" />
      </div>

      <div class="ingredient-rows">
        {#each ['75%', '60%', '85%', '50%', '70%', '65%'] as width, i (i)}
          <div class="ingredient-row">
            <Skeleton width="3.5rem" height="1rem" />
            <Skeleton {width} height="1rem" />
          </div>
        {/each}
      </div>
    </section>

    <section class="steps" aria-hidden="true">
      <div class="section-head">
        <Skeleton width="5rem" height="1.5rem" />
      </div>

      <div class="step-items">
        {#each [0, 1, 2] as step (step)}
          <div class="step-card">
            <div class="step-header">
              <Skeleton shape="circle" width="1.75rem" height="1.75rem" />
              <Skeleton width="4rem" height="1rem" />
            </div>
            <div class="step-text">
              <Skeleton width="100%" height="1rem" />
              <Skeleton width="92%" height="1rem" />
              <Skeleton width="60%" height="1rem" />
            </div>
          </div>
        {/each}
      </div>
    </section>
  </div>
</article>

<style>
  .surface {
    display: flex;
    flex-direction: column;
    gap: var(--space-8);
  }

  .hero {
    aspect-ratio: 16 / 9;
    max-height: 24rem;
    width: 100%;
    overflow: hidden;
    border-radius: var(--radius-lg);
  }

  .head {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .titleRow {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto;
    align-items: center;
    gap: var(--space-4);
  }

  .actions {
    display: flex;
    align-items: center;
    gap: var(--space-2);
  }

  .description {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    margin-top: var(--space-2);
    max-width: var(--measure);
  }

  .servings {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-4);
    padding-block: var(--space-6);
    border-block: 1px solid var(--border);
  }

  .body {
    --ingredients-column: 20rem;
    --card-padding: clamp(1rem, 2vw, 1.5rem);
    display: grid;
    grid-template-columns: minmax(0, var(--ingredients-column)) minmax(0, 1fr);
    gap: var(--layout-section-gap);
    align-items: start;
  }

  .ingredients {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    padding: var(--card-padding);
    background: var(--surface-sunken);
    border-radius: var(--radius-lg);
  }

  .section-head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-3);
    min-height: var(--control-sm);
  }

  .ingredient-rows {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .ingredient-row {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    min-height: 1.5rem;
  }

  .steps {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
  }

  .steps > .section-head {
    padding-block-start: var(--card-padding);
  }

  .step-items {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
  }

  .step-card {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    padding-bottom: var(--space-4);
    border-bottom: 1px solid var(--border);
  }

  .step-header {
    display: flex;
    align-items: center;
    gap: var(--space-3);
  }

  .step-text {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }

  @media screen and (width < 64rem) {
    .body {
      grid-template-columns: 1fr;
      gap: var(--space-8);
    }

    .steps > .section-head {
      padding-block-start: 0;
    }
  }

  @media (width < 52rem) {
    .titleRow {
      grid-template-columns: minmax(0, 1fr);
    }
  }
</style>
