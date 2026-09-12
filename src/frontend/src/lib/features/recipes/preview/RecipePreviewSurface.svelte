<script lang="ts">
  import { Button, Checkbox, Stepper } from '$ds';
  import { formatNumber, m } from '$shell/i18n';
  import type { PreviewRecipe } from './recipes';
  interface Props {
    recipe: PreviewRecipe;
    onback: () => void;
  }
  let { recipe, onback }: Props = $props();
  let servings = $state(2);
  let cooking = $state(false);
  let currentStep = $state(0);
  let checked = $state<Record<string, boolean>>({});
  function setCooking(value: boolean) {
    cooking = value;
  }
</script>

<div class="surface" class:cooking>
  <div class="toolbar">
    <Button variant="ghost" onclick={onback}>← {m['preview.back']()}</Button>
    <Button variant={cooking ? 'secondary' : 'primary'} onclick={() => setCooking(!cooking)}>
      {cooking ? m['preview.finish']() : m['preview.cook']()}
    </Button>
  </div>
  <header>
    <p class="eyebrow">{recipe.tag} · {m['preview.minutes']({ count: recipe.minutes })}</p>
    <h1 tabindex="-1">{recipe.title}</h1>
    <p class="description">{recipe.description}</p>
  </header>
  <div class="workspace">
    <section class="ingredients" aria-labelledby="ingredients-heading">
      <div class="section-heading">
        <h2 id="ingredients-heading">{m['preview.ingredients']()}</h2>
        <span>{m['preview.servings']()}</span>
      </div>
      <Stepper
        id="preview-servings"
        label={m['preview.servings']()}
        decreaseLabel={m['preview.less']()}
        increaseLabel={m['preview.more']()}
        bind:value={servings}
        min={1}
        max={12}
      />
      <ul>
        {#each recipe.ingredients as ingredient (ingredient.name)}
          <li>
            <Checkbox
              checked={checked[ingredient.name] ?? false}
              label={ingredient.name}
              onchange={(value) => (checked[ingredient.name] = value)}
            />
            <span class="amount" class:checked={checked[ingredient.name]}
              >{formatNumber((ingredient.quantity * servings) / 2, { maximumFractionDigits: 2 })}
              {ingredient.unit}</span
            >
          </li>
        {/each}
      </ul>
      <p class="note">{m['preview.scalingNote']()}</p>
    </section>
    <section class="method" aria-labelledby="method-heading">
      <div class="section-heading">
        <h2 id="method-heading">{m['preview.method']()}</h2>
        {#if cooking}<span aria-live="polite"
            >{m['preview.progress']({ current: currentStep + 1, total: recipe.steps.length })}</span
          >{/if}
      </div>
      <ol>
        {#each recipe.steps as step, index (index)}
          <li
            class:current={cooking && index === currentStep}
            class:receded={cooking && index !== currentStep}
            aria-current={cooking && index === currentStep ? 'step' : undefined}
          >
            <span class="number" aria-hidden="true">{String(index + 1).padStart(2, '0')}</span>
            <p>{step}</p>
          </li>
        {/each}
      </ol>
      {#if cooking}
        <div class="step-controls">
          <Button disabled={currentStep === 0} onclick={() => currentStep--}
            >{m['preview.previous']()}</Button
          >
          <Button
            variant="primary"
            onclick={() =>
              currentStep < recipe.steps.length - 1 ? currentStep++ : setCooking(false)}
            >{currentStep === recipe.steps.length - 1
              ? m['preview.done']()
              : m['preview.next']()}</Button
          >
        </div>
      {/if}
    </section>
  </div>
</div>

<style>
  .surface {
    max-width: var(--layout-wide);
    margin-inline: auto;
    padding: var(--space-8) var(--space-8) var(--space-16);
  }
  .toolbar {
    display: flex;
    justify-content: space-between;
    gap: var(--space-3);
    margin-bottom: var(--space-8);
  }
  header {
    max-width: 52rem;
    padding-bottom: var(--space-12);
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
  }
  .description {
    margin-top: var(--space-4);
    color: var(--text-muted);
    max-width: var(--measure);
  }
  .workspace {
    display: grid;
    grid-template-columns: 1fr 1.5fr;
    gap: var(--space-16);
  }
  .section-heading {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: var(--space-3);
    margin-bottom: var(--space-6);
  }
  h2 {
    font-size: var(--text-xl);
  }
  .section-heading span,
  .note {
    font-size: var(--text-sm);
    color: var(--text-muted);
  }
  ul,
  ol {
    list-style: none;
    padding: 0;
    margin: var(--space-6) 0;
  }
  .ingredients li {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-4);
    min-height: var(--control-lg);
    border-bottom: 1px solid var(--border);
  }
  .amount {
    white-space: nowrap;
    font-variant-numeric: tabular-nums;
    font-weight: var(--weight-medium);
  }
  .checked {
    text-decoration: line-through;
    color: var(--text-muted);
  }
  .method li {
    display: flex;
    align-items: baseline;
    gap: var(--space-4);
    padding-bottom: var(--space-8);
  }
  .number {
    flex-shrink: 0;
    color: var(--text-subtle);
    font-size: var(--text-sm);
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
  .receded {
    color: var(--text-muted);
  }
  .current .number {
    color: var(--accent);
  }
  .step-controls :global(button) {
    min-height: var(--control-lg);
  }
  .step-controls {
    display: flex;
    justify-content: space-between;
    gap: var(--space-4);
    position: sticky;
    bottom: 0;
    background: var(--surface);
    padding-block: var(--space-4);
  }
  @media (max-width: 47.999rem) {
    .surface {
      padding: var(--space-4) var(--space-6) var(--space-12);
    }
    .workspace {
      grid-template-columns: 1fr;
      gap: var(--space-8);
    }
    header {
      padding-bottom: var(--space-8);
    }
  }
</style>
