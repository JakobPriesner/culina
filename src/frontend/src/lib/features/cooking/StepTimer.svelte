<script lang="ts">
  import { Button, IconButton } from '$ds';

  import { m } from '$shell/i18n';
  import type { KitchenTimer } from './timers.svelte';

  /**
   * The timer for one step, only where the recipe says it is a wait; it goes off in place, not in a
   * dialog.
   */
  interface Props {
    durationSeconds: number;
    timer: KitchenTimer | undefined;
    secondsLeft: number;
    onstart: () => void;
    ondismiss: () => void;
    onpause?: () => void;
    onresume?: () => void;
    /** The cook dock's controls are all large; the floating window's are small. */
    size?: 'sm' | 'lg';
  }

  let {
    durationSeconds,
    timer,
    secondsLeft,
    onstart,
    ondismiss,
    onpause,
    onresume,
    size = 'sm'
  }: Props = $props();

  const minutes = $derived(Math.floor(secondsLeft / 60));
  const seconds = $derived(String(secondsLeft % 60).padStart(2, '0'));
</script>

{#if !timer}
  <Button {size} onclick={onstart}>
    {m['cooking.timer.start']({ minutes: Math.round(durationSeconds / 60) })}
  </Button>
{:else if secondsLeft > 0}
  <div class="timer-controls">
    <p class="running" role="timer" aria-live="off">
      {timer.pausedRemaining !== undefined
        ? m['cooking.timer.paused']({ minutes, seconds })
        : m['cooking.timer.running']({ minutes, seconds })}
    </p>
    {#if timer.pausedRemaining !== undefined && onresume}
      <Button {size} onclick={onresume}>{m['cooking.timer.resume']()}</Button>
    {:else if onpause}
      <Button {size} onclick={onpause}>{m['cooking.timer.pause']()}</Button>
    {/if}
    <!-- A timer started by mistake (one tap from the step's words) is taken back, not left to ring. -->
    <IconButton {size} label={m['cooking.timer.cancel']()} onclick={ondismiss}>
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <path d="m7 7 10 10M17 7 7 17" stroke-linecap="round" />
      </svg>
    </IconButton>
  </div>
{:else}
  <!-- Assertive: this one cannot wait, the pan is on. -->
  <p class="done" role="alert">
    {m['cooking.timer.done']()}
    <button class="dismiss" type="button" onclick={ondismiss}>
      {m['cooking.timer.dismiss']()}
    </button>
  </p>
{/if}

<style>
  .timer-controls {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
  }

  /* Time left, pause and cancel share a phone's row. */
  .timer-controls > :global(.button) {
    padding-inline: var(--space-4);
  }

  .running {
    font-variant-numeric: tabular-nums;
    font-weight: var(--weight-semibold);
  }

  .done {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    font-weight: var(--weight-semibold);
    color: var(--text-danger);
  }

  .dismiss {
    padding: 0;
    border: none;
    background: none;
    color: var(--accent);
    font: inherit;
    font-weight: var(--weight-medium);
    cursor: pointer;
  }
</style>
