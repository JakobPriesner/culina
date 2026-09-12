<script lang="ts">
  import { Button } from '$ds';

  import { m } from '$shell/i18n';
  import type { KitchenTimer } from './timers.svelte';

  /**
   * The timer for one step.
   *
   * Only where the recipe says the step is a wait — a timer button on "chop the
   * onion" is noise. When it goes off it says so in place rather than throwing
   * a dialog at someone holding a hot pan.
   */
  interface Props {
    durationSeconds: number;
    timer: KitchenTimer | undefined;
    secondsLeft: number;
    onstart: () => void;
    ondismiss: () => void;
  }

  let { durationSeconds, timer, secondsLeft, onstart, ondismiss }: Props = $props();

  const minutes = $derived(Math.floor(secondsLeft / 60));
  const seconds = $derived(String(secondsLeft % 60).padStart(2, '0'));
</script>

{#if !timer}
  <Button size="sm" onclick={onstart}>
    {m['cooking.timer.start']({ minutes: Math.round(durationSeconds / 60) })}
  </Button>
{:else if secondsLeft > 0}
  <p class="running" role="timer" aria-live="off">
    {m['cooking.timer.running']({ minutes, seconds })}
  </p>
{:else}
  <!-- Assertive, because this one genuinely cannot wait: the pan is on. -->
  <p class="done" role="alert">
    {m['cooking.timer.done']()}
    <button class="dismiss" type="button" onclick={ondismiss}>
      {m['cooking.timer.dismiss']()}
    </button>
  </p>
{/if}

<style>
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
