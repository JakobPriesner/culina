<script lang="ts">
  import { m } from '$shell/i18n';

  /** The steps of the previewed recipe: all of them, or only the current one while cooking. */
  interface Props {
    steps: readonly string[];
    cooking: boolean;
    currentStep: number;
    /** How tall the dock under the steps is, so none scrolls in beneath it. */
    dockHeight: number;
  }

  let { steps, cooking, currentStep, dockHeight }: Props = $props();
</script>

<section
  class="method"
  aria-labelledby="method-heading"
  style:--preview-dock-height="{dockHeight}px"
>
  <div class="section-heading">
    <h2 id="method-heading">{m['preview.method']()}</h2>
    {#if cooking}<span aria-live="polite"
        >{m['preview.progress']({ current: currentStep + 1, total: steps.length })}</span
      >{/if}
  </div>
  {#if cooking}
    <div class="progress" aria-hidden="true">
      {#each steps as _, index (index)}<span class:complete={index <= currentStep}></span>{/each}
    </div>
  {/if}
  <ol>
    {#each steps as step, index (index)}
      <li
        hidden={cooking && index !== currentStep}
        class:current={cooking && index === currentStep}
        aria-current={cooking && index === currentStep ? 'step' : undefined}
        tabindex="-1"
      >
        <span class="number" aria-hidden="true">{String(index + 1).padStart(2, '0')}</span>
        <p>{step}</p>
      </li>
    {/each}
  </ol>
</section>

<style>
  h2 {
    font-family: var(--font-editorial);
    font-weight: var(--weight-regular);
    font-size: var(--text-2xl);
    letter-spacing: -0.025em;
  }

  .section-heading {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: var(--space-3);
    margin-bottom: var(--space-6);
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

  li {
    display: flex;
    align-items: baseline;
    gap: var(--space-4);
    padding-block: var(--space-6);
    border-top: 1px solid var(--border);
    scroll-margin-block: var(--space-8) calc(var(--preview-dock-height) + var(--space-4));
  }

  li[hidden] {
    display: none;
  }

  .number {
    flex-shrink: 0;
    color: var(--accent);
    font-family: var(--font-editorial);
    font-size: var(--text-2xl);
    font-variant-numeric: tabular-nums;
  }

  li p {
    font-size: var(--text-lg);
    line-height: var(--leading-relaxed);
  }

  li.current p {
    font-size: var(--text-cook);
    line-height: var(--leading-normal);
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

  @media (width < 64rem) {
    li.current {
      gap: var(--space-3);
    }
  }
</style>
