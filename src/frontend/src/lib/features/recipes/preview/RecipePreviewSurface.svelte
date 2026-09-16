<script lang="ts">
  import { tick } from 'svelte';
  import { Button, Image, Sheet } from '$ds';
  import { m } from '$shell/i18n';
  import type { PreviewProgress, PreviewRecipe } from './recipes';
  import PreviewIngredients from './PreviewIngredients.svelte';

  let {
    recipe,
    progress,
    onprogress,
    onback
  }: {
    recipe: PreviewRecipe;
    progress: PreviewProgress;
    onprogress: (value: PreviewProgress) => void;
    onback: () => void;
  } = $props();
  let ingredientsOpen = $state(false);
  let dockHeight = $state(0);
  let surface: HTMLDivElement;
  const cooking = $derived(progress.cooking);

  async function changeStep(currentStep: number) {
    onprogress({ ...progress, currentStep });
    await tick();
    surface.querySelector<HTMLElement>('[aria-current="step"]')?.focus();
  }

  async function setCooking(value: boolean) {
    onprogress({ ...progress, cooking: value });
    await tick();
    const target = surface.querySelector<HTMLElement>(value ? '[aria-current="step"]' : 'h1');
    target?.focus();
    target?.scrollIntoView({ block: 'nearest' });
  }
</script>

<div bind:this={surface} class="surface" class:cooking style:--preview-dock-height="{dockHeight}px">
  <div class="toolbar">
    <Button variant="ghost" size="sm" onclick={onback}>← {m['preview.back']()}</Button>
    <Button
      size="sm"
      variant={cooking ? 'secondary' : 'primary'}
      onclick={() => void setCooking(!cooking)}
    >
      {cooking ? m['preview.finish']() : m['preview.cook']()}
    </Button>
  </div>
  <header class="recipe-header">
    <div class="intro">
      <p class="eyebrow">{recipe.tag} · {m['preview.minutes']({ count: recipe.minutes })}</p>
      <h1 tabindex="-1">{recipe.title}</h1>
      {#if !cooking}<p class="description">{recipe.description}</p>{/if}
    </div>
    {#if !cooking && recipe.image}
      <div class="recipe-photo"><Image src={recipe.image} alt="" fill loading="eager" /></div>
    {/if}
  </header>
  <div class="workspace">
    <section class="ingredients" aria-labelledby="ingredients-heading">
      <h2 id="ingredients-heading">{m['preview.ingredients']()}</h2>
      <PreviewIngredients {recipe} {progress} {onprogress} />
    </section>
    <section class="method" aria-labelledby="method-heading">
      <div class="section-heading">
        <h2 id="method-heading">{m['preview.method']()}</h2>
        {#if cooking}<span aria-live="polite"
            >{m['preview.progress']({
              current: progress.currentStep + 1,
              total: recipe.steps.length
            })}</span
          >{/if}
      </div>
      {#if cooking}
        <div class="progress" aria-hidden="true">
          {#each recipe.steps as _, index (index)}<span
              class:complete={index <= progress.currentStep}
            ></span>{/each}
        </div>
      {/if}
      <ol>
        {#each recipe.steps as step, index (index)}
          <li
            hidden={cooking && index !== progress.currentStep}
            class:current={cooking && index === progress.currentStep}
            aria-current={cooking && index === progress.currentStep ? 'step' : undefined}
            tabindex="-1"
          >
            <span class="number" aria-hidden="true">{String(index + 1).padStart(2, '0')}</span>
            <p>{step}</p>
          </li>
        {/each}
      </ol>
    </section>
  </div>
  {#if cooking}
    <div class="dock" bind:clientHeight={dockHeight}>
      <div class="step-controls">
        <Button
          size="sm"
          disabled={progress.currentStep === 0}
          onclick={() => void changeStep(progress.currentStep - 1)}
          >{m['preview.previous']()}</Button
        >
        <div class="mobile-ingredients">
          <Button size="sm" onclick={() => (ingredientsOpen = true)}
            >{m['preview.ingredients']()}</Button
          >
        </div>
        <Button
          size="sm"
          variant="primary"
          label={progress.currentStep === recipe.steps.length - 1 ? m['preview.done']() : undefined}
          onclick={() =>
            progress.currentStep < recipe.steps.length - 1
              ? void changeStep(progress.currentStep + 1)
              : void setCooking(false)}
        >
          {progress.currentStep === recipe.steps.length - 1
            ? m['preview.doneShort']()
            : m['preview.next']()}
          <span aria-hidden="true"
            >{progress.currentStep === recipe.steps.length - 1 ? '✓' : '→'}</span
          >
        </Button>
      </div>
    </div>
  {/if}
</div>

<Sheet
  bind:open={ingredientsOpen}
  title={m['preview.ingredients']()}
  closeLabel={m['preview.closeIngredients']()}
>
  {#if ingredientsOpen}<PreviewIngredients {recipe} {progress} {onprogress} />{/if}
</Sheet>

<style>
  .surface {
    max-width: var(--layout-wide);
    margin-inline: auto;
    padding: var(--space-6) var(--layout-gutter-end) var(--space-16) var(--layout-gutter-start);
  }
  .toolbar {
    display: flex;
    flex-wrap: wrap;
    justify-content: space-between;
    gap: var(--space-3);
    margin-bottom: var(--space-8);
  }
  .recipe-header {
    display: grid;
    grid-template-columns: minmax(0, 1.5fr) minmax(0, 1fr);
    align-items: center;
    gap: var(--layout-section-gap);
    margin-bottom: var(--space-12);
  }
  .recipe-photo {
    height: clamp(12rem, 24vw, 20rem);
  }
  .eyebrow {
    font-size: var(--text-sm);
    color: var(--text-muted);
    margin-bottom: var(--space-4);
  }
  h1 {
    font-family: var(--font-editorial);
    font-size: var(--text-display);
    font-weight: var(--weight-regular);
    letter-spacing: -0.04em;
    line-height: var(--leading-tight);
  }
  .description {
    margin-top: var(--space-4);
    color: var(--text-muted);
    max-width: var(--measure);
  }
  .workspace {
    display: grid;
    grid-template-columns: minmax(0, 1fr) minmax(0, 1.5fr);
    align-items: start;
    gap: var(--layout-section-gap);
  }
  h2 {
    font-family: var(--font-editorial);
    font-weight: var(--weight-regular);
    font-size: var(--text-2xl);
    letter-spacing: -0.025em;
  }
  .ingredients {
    padding: var(--space-6);
    border-radius: var(--radius-lg);
    background: var(--surface-sunken);
  }
  .ingredients h2,
  .section-heading {
    margin-bottom: var(--space-6);
  }
  .section-heading {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: var(--space-3);
  }
  .section-heading span {
    font-size: var(--text-sm);
    color: var(--text-muted);
  }
  ol {
    list-style: none;
    padding: 0;
    margin: var(--space-6) 0;
  }
  .method li {
    display: flex;
    align-items: baseline;
    gap: var(--space-4);
    padding-block: var(--space-6);
    border-top: 1px solid var(--border);
    scroll-margin-block: var(--space-8) calc(var(--preview-dock-height) + var(--space-4));
  }
  .method li[hidden] {
    display: none;
  }
  .number {
    flex-shrink: 0;
    color: var(--accent);
    font-family: var(--font-editorial);
    font-size: var(--text-2xl);
    font-variant-numeric: tabular-nums;
  }
  .method li p {
    font-size: var(--text-lg);
    line-height: var(--leading-relaxed);
  }
  .method li.current p {
    font-size: var(--text-cook);
    line-height: var(--leading-normal);
  }
  .current .number {
    color: var(--accent);
  }
  .progress {
    display: flex;
    gap: var(--space-2);
  }
  .progress span {
    flex: 1;
    height: var(--space-1);
    border-radius: var(--radius-full);
    background: var(--border);
  }
  .progress .complete {
    background: var(--accent);
  }
  .cooking {
    padding-bottom: calc(var(--preview-dock-height) + var(--space-8));
  }
  .cooking .recipe-header {
    display: block;
    margin-bottom: var(--space-8);
    padding-bottom: var(--space-6);
    border-bottom: 1px solid var(--border);
  }
  .cooking h1 {
    font-size: var(--text-3xl);
  }
  .cooking .eyebrow {
    margin-bottom: var(--space-2);
  }
  .dock {
    position: fixed;
    bottom: 0;
    inset-inline: 0;
    z-index: 2;
    background: var(--surface);
    border-top: 1px solid var(--border);
  }
  .step-controls {
    display: flex;
    justify-content: space-between;
    gap: var(--space-3);
    max-width: var(--layout-wide);
    margin-inline: auto;
    padding: var(--space-3) var(--layout-gutter-end)
      calc(var(--space-3) + env(safe-area-inset-bottom, 0px)) var(--layout-gutter-start);
  }
  .step-controls :global(button) {
    min-height: var(--control-lg);
  }
  .mobile-ingredients {
    display: none;
  }
  @media (width < 64rem) {
    .surface {
      padding: var(--space-4) var(--layout-gutter-end) var(--space-12) var(--layout-gutter-start);
    }
    .toolbar {
      gap: var(--space-1);
      margin-bottom: var(--space-6);
    }
    .recipe-header {
      grid-template-columns: 1fr;
      gap: var(--space-6);
      margin-bottom: var(--space-8);
    }
    .recipe-photo {
      height: auto;
      aspect-ratio: 16 / 9;
    }
    .workspace {
      grid-template-columns: 1fr;
      gap: var(--space-8);
    }
    .cooking {
      padding-bottom: calc(var(--preview-dock-height) + var(--space-8));
    }
    .cooking .ingredients {
      display: none;
    }
    .cooking .recipe-header {
      margin-bottom: var(--space-6);
    }
    .cooking h1 {
      font-size: var(--text-2xl);
    }
    .mobile-ingredients {
      display: block;
    }
    .step-controls {
      gap: var(--space-2);
      padding-inline: var(--layout-gutter-start) var(--layout-gutter-end);
    }
    .method li.current {
      gap: var(--space-3);
    }
  }
  @media (max-width: 23.999rem) {
    .ingredients {
      padding: var(--space-4);
    }
    .step-controls :global(button) {
      padding-inline: var(--space-2);
    }
  }
  @media screen and (max-height: 32rem) {
    .dock {
      position: static;
      margin-top: var(--space-6);
    }
    .cooking {
      padding-bottom: var(--space-8);
    }
  }
</style>
