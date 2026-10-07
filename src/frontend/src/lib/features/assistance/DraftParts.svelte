<script lang="ts">
  import { Checkbox } from '$ds';
  import { m } from '$shell/i18n';

  import type { DraftPart } from './draftParts';
  import type { Accepted, Draft } from './draftToRecipe';

  /** The review rows: a tick box per part, before beside after, then the steps written. */
  interface Props {
    parts: readonly DraftPart[];
    accepted: Accepted;
    steps: Draft['steps'];
    onchange: (key: keyof Accepted, checked: boolean) => void;
  }

  let { parts, accepted, steps, onchange }: Props = $props();
</script>

<ul class="parts">
  {#each parts as { key, label, before, after } (key)}
    <li class="part arrival">
      <Checkbox checked={accepted[key]} {label} onchange={(checked) => onchange(key, checked)} />

      <div class="compare">
        <p class="side">
          <span class="which">{m['assist.before']()}</span>
          <span class="was">{before || m['assist.empty']()}</span>
        </p>
        <p class="side">
          <span class="which">{m['assist.after']()}</span>
          <span>{after || m['assist.empty']()}</span>
        </p>
      </div>
    </li>
  {/each}
</ul>

{#if steps.length > 0}
  <ol class="steps">
    {#each steps as step, index (index)}
      <li class="arrival">
        {#if step.title}<span class="stepTitle">{step.title}</span>{/if}
        {step.text}
      </li>
    {/each}
  </ol>
{/if}

<style>
  .parts {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    margin-top: var(--space-4);
    list-style: none;
  }

  .part {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    padding-bottom: var(--space-4);
    border-bottom: 1px solid var(--border);
  }

  /* Side by side where there is room (comparing is the job), stacked on a phone. */
  .compare {
    display: grid;
    gap: var(--space-2);
    padding-left: var(--space-6);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  .side {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 0;
  }

  .which {
    color: var(--text-subtle);
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .was {
    color: var(--text-muted);
  }

  .steps {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    margin-top: var(--space-4);
    padding-left: var(--space-6);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  .stepTitle {
    display: block;
    font-weight: var(--weight-semibold);
  }

  .arrival {
    animation: arrive var(--duration-base) var(--ease-out) both;
  }

  @keyframes arrive {
    from {
      opacity: 0;
      transform: translateY(var(--space-1));
    }

    to {
      opacity: 1;
      transform: translateY(0);
    }
  }

  @media (min-width: 40rem) {
    .compare {
      grid-template-columns: 1fr 1fr;
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .arrival {
      animation: none;
    }
  }
</style>
