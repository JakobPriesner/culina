<script lang="ts">
  import { onMount, tick } from 'svelte';
  import { m } from '$shell/i18n';

  interface Props {
    anchor: HTMLElement;
    name: string;
    stepAmount: string;
    totalAmount?: string | null;
    note?: string | null;
    onclose: () => void;
    onlocate?: () => void;
  }

  let {
    anchor,
    name,
    stepAmount,
    totalAmount = null,
    note = null,
    onclose,
    onlocate
  }: Props = $props();

  let card = $state<HTMLDivElement>();
  let coords = $state({ top: 0, left: 0 });

  const hasDifferentTotal = $derived(
    Boolean(totalAmount && totalAmount.trim() !== '' && totalAmount.trim() !== stepAmount.trim())
  );

  function position() {
    if (!card || !anchor) return;

    const from = anchor.getBoundingClientRect();
    const self = card.getBoundingClientRect();
    const gap = 8;
    const margin = 12;

    const spaceBelow = window.innerHeight - from.bottom;
    const spaceAbove = from.top;
    const placeAbove = self.height + gap > spaceBelow && spaceAbove > spaceBelow;

    let top = placeAbove ? from.top - gap - self.height : from.bottom + gap;
    let left = from.left + from.width / 2 - self.width / 2;

    // Clamp horizontally
    left = Math.max(margin, Math.min(left, window.innerWidth - margin - self.width));
    // Clamp vertically
    top = Math.max(margin, Math.min(top, window.innerHeight - margin - self.height));

    coords = { top, left };
  }

  function handleKeydown(event: KeyboardEvent) {
    if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      onclose();
    }
  }

  function handlePointerDown(event: PointerEvent) {
    if (!card || !anchor) return;
    const target = event.target as Node;
    if (!card.contains(target) && !anchor.contains(target)) {
      onclose();
    }
  }

  onMount(() => {
    void tick().then(position);

    window.addEventListener('keydown', handleKeydown, { capture: true });
    window.addEventListener('pointerdown', handlePointerDown, { capture: true });
    window.addEventListener('resize', position, { passive: true });
    window.addEventListener('scroll', position, { capture: true, passive: true });

    return () => {
      window.removeEventListener('keydown', handleKeydown, { capture: true });
      window.removeEventListener('pointerdown', handlePointerDown, { capture: true });
      window.removeEventListener('resize', position);
      window.removeEventListener('scroll', position, { capture: true });
    };
  });
</script>

<div
  bind:this={card}
  class="quick-look"
  role="dialog"
  aria-label={name}
  style:top="{coords.top}px"
  style:left="{coords.left}px"
>
  <div class="header">
    <h4 class="name">{name}</h4>
    <button
      type="button"
      class="close-btn"
      aria-label={m['recipe.quickLookClose']()}
      onclick={onclose}
    >
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
        <path d="M18 6 6 18M6 6l12 12" stroke-linecap="round" />
      </svg>
    </button>
  </div>

  {#if stepAmount}
    <p class="amount">{stepAmount}</p>
  {/if}

  <!-- Only when the step takes part of it. Saying "all of it" otherwise would
       be a claim the card cannot back: another step may well use it too. -->
  {#if hasDifferentTotal && totalAmount}
    <p class="total">{m['recipe.quickLookTotal']({ amount: totalAmount })}</p>
  {/if}

  {#if note}
    <p class="note">{note}</p>
  {/if}

  {#if onlocate}
    <button
      type="button"
      class="locate-btn"
      onclick={() => {
        onlocate?.();
        onclose();
      }}
    >
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
        <circle cx="12" cy="10" r="3" />
      </svg>
      <span>{m['recipe.quickLookShowInList']()}</span>
    </button>
  {/if}
</div>

<style>
  /*
   * The card is written inside the step's paragraph, next to the reference it
   * belongs to, so it inherits everything that paragraph says about text —
   * the significant whitespace above all, which would turn every line break in
   * this markup into a blank line inside the card, and the bold or italic of a
   * reference written inside emphasis. It resets all of that to its own.
   */
  .quick-look {
    position: fixed;
    z-index: var(--z-overlay);
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    width: min(16rem, calc(100vw - 24px));
    padding: var(--space-3) var(--space-4) var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-overlay-glass);
    backdrop-filter: blur(20px) saturate(180%);
    -webkit-backdrop-filter: blur(20px) saturate(180%);
    box-shadow: var(--shadow-overlay);
    animation: popoverIn var(--duration-fast) var(--ease-out);
    color: var(--text);
    font-size: var(--text-sm);
    font-style: normal;
    font-weight: var(--weight-regular);
    line-height: var(--leading-normal);
    text-align: start;
    white-space: normal;
  }

  @keyframes popoverIn {
    from {
      opacity: 0;
      transform: scale(0.96) translateY(3px);
    }
    to {
      opacity: 1;
      transform: scale(1) translateY(0);
    }
  }

  .header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2);
  }

  .name {
    min-width: 0;
    margin: 0;
    overflow: hidden;
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    line-height: var(--leading-tight);
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .close-btn {
    display: flex;
    flex: none;
    align-items: center;
    justify-content: center;
    width: 1.5rem;
    height: 1.5rem;
    margin-inline-end: calc(-1 * var(--space-2));
    padding: 0;
    border: none;
    border-radius: var(--radius-full);
    background: transparent;
    color: var(--text-subtle);
    cursor: pointer;
    transition:
      background-color var(--duration-fast) var(--ease-out),
      color var(--duration-fast) var(--ease-out);
  }

  .close-btn:hover {
    background: var(--surface-hover);
    color: var(--text);
  }

  .close-btn svg {
    width: 0.875rem;
    height: 0.875rem;
  }

  /* What the step asks for, as the one thing on the card worth reading from
     across the counter. */
  .amount {
    margin: 0;
    font-size: var(--text-xl);
    font-weight: var(--weight-semibold);
    font-variant-numeric: tabular-nums;
    line-height: var(--leading-tight);
  }

  .total,
  .note {
    margin: 0;
    color: var(--text-muted);
  }

  .total {
    font-variant-numeric: tabular-nums;
  }

  .locate-btn {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: var(--space-2);
    width: 100%;
    margin-top: var(--space-2);
    padding: var(--space-2) var(--space-3);
    border: none;
    border-radius: var(--radius-md);
    background: var(--surface-hover);
    color: var(--text);
    font: inherit;
    font-size: var(--text-xs);
    font-weight: var(--weight-medium);
    cursor: pointer;
    transition:
      background-color var(--duration-fast) var(--ease-out),
      color var(--duration-fast) var(--ease-out);
  }

  .locate-btn:hover {
    background: var(--surface-selected);
    color: var(--accent);
  }

  .locate-btn svg {
    width: 0.875rem;
    height: 0.875rem;
  }
</style>
