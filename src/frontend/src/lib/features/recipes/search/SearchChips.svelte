<script lang="ts">
  import { m } from '$shell/i18n';

  import type { SearchChip } from '../types';
  import { chipLabel } from './wording';

  /**
   * What a query was understood to mean, each reading removable where it
   * stands.
   *
   * The chips are the parser's confession: without them, reading a sentence
   * for a diet and a time is a system quietly changing what was asked for, and
   * the first wrong guess is the last time anybody trusts the box. With them a
   * wrong guess is one tap to undo — and seeing "unter 30 Minuten" become a
   * chip once is how somebody learns to type it on purpose.
   *
   * Drawn like the toolbar's own applied filters, because to the person they
   * are the same thing: something narrowing the list, with an × to stop it.
   */
  interface Props {
    chips: readonly SearchChip[];
    onremove: (chip: SearchChip) => void;
  }

  let { chips, onremove }: Props = $props();
</script>

{#if chips.length > 0}
  <ul class="chips" aria-label={m['search.chips']()}>
    {#each chips as chip (`${chip.kind}:${chip.start}`)}
      <li>
        <button
          type="button"
          class="chip {chip.kind}"
          aria-label={m['search.chip.remove']({ label: chipLabel(chip) })}
          onclick={() => onremove(chip)}
        >
          {chipLabel(chip)}
          <span aria-hidden="true">×</span>
        </button>
      </li>
    {/each}
  </ul>
{/if}

<style>
  .chips {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .chip {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
    min-height: var(--control-sm);
    padding: 0 var(--space-3);
    border: 1px solid var(--border);
    border-radius: var(--radius-full);
    background: var(--surface-sunken);
    color: var(--text);
    font: inherit;
    font-size: var(--text-sm);
    cursor: pointer;
    animation: arrive var(--duration-fast) var(--ease-out);
  }

  .chip:hover {
    border-color: var(--border-strong);
  }

  /* What is left out reads as a subtraction, not as one more filter. */
  .exclusion {
    background: transparent;
    border-style: dashed;
  }

  @keyframes arrive {
    from {
      opacity: 0;
      transform: scale(0.9);
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .chip {
      animation: none;
    }
  }
</style>
