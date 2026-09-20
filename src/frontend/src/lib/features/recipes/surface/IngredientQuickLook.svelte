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
    <div class="title-group">
      <span class="badge">@</span>
      <h4 class="name">{name}</h4>
    </div>
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

  <div class="metrics">
    <div class="metric-primary">
      <span class="metric-label">{m['recipe.stepNeeds']()}</span>
      <span class="metric-value">{stepAmount}</span>
    </div>

    {#if hasDifferentTotal && totalAmount}
      <div class="metric-secondary">
        <span class="metric-label"
          >{m['recipe.quickLookTotal']({ amount: '' }).replace(/:\s*$/, '')}</span
        >
        <span class="metric-value subtle">{totalAmount}</span>
      </div>
    {:else}
      <div class="metric-secondary">
        <span class="pill-all">{m['recipe.quickLookAllHere']()}</span>
      </div>
    {/if}
  </div>

  {#if note}
    <p class="note">
      <svg class="note-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <circle cx="12" cy="12" r="10" />
        <path d="M12 16v-4M12 8h.01" stroke-linecap="round" />
      </svg>
      <span>{note}</span>
    </p>
  {/if}

  {#if onlocate}
    <div class="actions">
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
    </div>
  {/if}
</div>

<style>
  .quick-look {
    position: fixed;
    z-index: var(--z-overlay, 100);
    width: min(18rem, calc(100vw - 24px));
    padding: var(--space-3) var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-overlay-glass);
    backdrop-filter: blur(20px) saturate(180%);
    -webkit-backdrop-filter: blur(20px) saturate(180%);
    box-shadow: var(--shadow-overlay);
    animation: popoverIn var(--duration-fast) var(--ease-out);
    color: var(--text);
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
    margin-bottom: var(--space-3);
  }

  .title-group {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    min-width: 0;
  }

  .badge {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 1.25rem;
    height: 1.25rem;
    border-radius: var(--radius-full);
    background: var(--surface-highlight);
    color: var(--accent);
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
  }

  .name {
    margin: 0;
    font-size: var(--text-base);
    font-weight: var(--weight-semibold);
    line-height: var(--leading-tight);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .close-btn {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 1.5rem;
    height: 1.5rem;
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

  .metrics {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: var(--space-3);
    padding: var(--space-2) var(--space-3);
    background: var(--surface-sunken);
    border-radius: var(--radius-md);
  }

  .metric-primary,
  .metric-secondary {
    display: flex;
    flex-direction: column;
  }

  .metric-label {
    font-size: 0.6875rem;
    letter-spacing: 0.06em;
    text-transform: uppercase;
    color: var(--text-subtle);
  }

  .metric-value {
    font-size: var(--text-base);
    font-weight: var(--weight-semibold);
    font-variant-numeric: tabular-nums;
    color: var(--text);
  }

  .metric-value.subtle {
    font-size: var(--text-sm);
    color: var(--text-muted);
  }

  .pill-all {
    font-size: var(--text-xs);
    font-weight: var(--weight-medium);
    color: var(--accent);
  }

  .note {
    display: flex;
    align-items: flex-start;
    gap: var(--space-2);
    margin: var(--space-2) 0 0;
    padding-inline: var(--space-1);
    color: var(--text-muted);
    font-size: var(--text-xs);
    line-height: var(--leading-normal);
  }

  .note-icon {
    flex: none;
    width: 0.875rem;
    height: 0.875rem;
    margin-top: 0.125rem;
    color: var(--text-subtle);
  }

  .actions {
    margin-top: var(--space-3);
    padding-top: var(--space-2);
    border-top: 1px solid var(--border);
  }

  .locate-btn {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: var(--space-2);
    width: 100%;
    padding: var(--space-2) var(--space-3);
    border: none;
    border-radius: var(--radius-md);
    background: var(--surface-hover);
    color: var(--text);
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
