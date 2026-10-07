<script lang="ts">
  import { tick } from 'svelte';
  import { Button, Sheet } from '$ds';
  import { m } from '$shell/i18n';
  import type { PreviewProgress, PreviewRecipe } from './recipes';
  import PreviewIngredients from './PreviewIngredients.svelte';
  import PreviewMethod from './PreviewMethod.svelte';
  import PreviewRecipeHeader from './PreviewRecipeHeader.svelte';
  import PreviewStepDock from './PreviewStepDock.svelte';

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

  const lastStep = $derived(progress.currentStep === recipe.steps.length - 1);
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
  <PreviewRecipeHeader {recipe} {cooking} />
  <div class="workspace">
    <section class="ingredients" aria-labelledby="ingredients-heading">
      <h2 id="ingredients-heading">{m['preview.ingredients']()}</h2>
      <PreviewIngredients {recipe} {progress} {onprogress} />
    </section>
    <PreviewMethod steps={recipe.steps} {cooking} currentStep={progress.currentStep} {dockHeight} />
  </div>
  {#if cooking}
    <PreviewStepDock
      currentStep={progress.currentStep}
      stepCount={recipe.steps.length}
      bind:height={dockHeight}
      onprevious={() => void changeStep(progress.currentStep - 1)}
      onnext={() => (lastStep ? void setCooking(false) : void changeStep(progress.currentStep + 1))}
      oningredients={() => (ingredientsOpen = true)}
    />
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

  .workspace {
    display: grid;
    grid-template-columns: minmax(0, 1fr) minmax(0, 1.5fr);
    align-items: start;
    gap: var(--layout-section-gap);
  }

  .ingredients {
    padding: var(--space-6);
    border-radius: var(--radius-lg);
    background: var(--surface-sunken);
  }

  .ingredients h2 {
    margin-bottom: var(--space-6);
    font-family: var(--font-editorial);
    font-weight: var(--weight-regular);
    font-size: var(--text-2xl);
    letter-spacing: -0.025em;
  }

  .cooking {
    padding-bottom: calc(var(--preview-dock-height) + var(--space-8));
  }

  @media (width < 64rem) {
    .surface {
      padding: var(--space-4) var(--layout-gutter-end) var(--space-12) var(--layout-gutter-start);
    }

    .toolbar {
      gap: var(--space-1);
      margin-bottom: var(--space-6);
    }

    .workspace {
      grid-template-columns: 1fr;
      gap: var(--space-8);
    }

    .cooking .ingredients {
      display: none;
    }
  }

  @media (max-width: 23.999rem) {
    .ingredients {
      padding: var(--space-4);
    }
  }

  @media screen and (max-height: 32rem) {
    .cooking {
      padding-bottom: var(--space-8);
    }
  }
</style>
