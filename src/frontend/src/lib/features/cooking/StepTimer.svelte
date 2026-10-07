<script lang="ts">
  import { Button } from '$ds';

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
  }

  let { durationSeconds, timer, secondsLeft, onstart, ondismiss, onpause, onresume }: Props =
    $props();

  const minutes = $derived(Math.floor(secondsLeft / 60));
  const seconds = $derived(String(secondsLeft % 60).padStart(2, '0'));
</script>

{#if !timer}
  <Button size="sm" onclick={onstart}>
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
      <Button size="sm" onclick={onresume}>{m['cooking.timer.resume']()}</Button>
    {:else if onpause}
      <Button size="sm" onclick={onpause}>{m['cooking.timer.pause']()}</Button>
    {/if}
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
