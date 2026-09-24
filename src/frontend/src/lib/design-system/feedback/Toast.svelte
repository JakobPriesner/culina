<script lang="ts">
  import type { Toast, ToastTone } from '$shell/toaster.svelte';

  import IconButton from '../actions/IconButton.svelte';

  /**
   * One message. Presentation only — the queue and the clock live in the
   * toaster, so a toast is the same whether it was raised by a page, a store or
   * a failed request.
   */
  interface Props {
    toast: Toast;
    dismissLabel: string;
    onact: (id: string) => void;
    ondismiss: (id: string) => void;
    onpause: (id: string) => void;
    onresume: (id: string) => void;
  }

  let { toast, dismissLabel, onact, ondismiss, onpause, onresume }: Props = $props();

  const tones: Record<ToastTone, string> = {
    neutral: 'neutral',
    success: 'success',
    danger: 'danger'
  };
</script>

<!--
  Pausing on hover *and* on focus: a keyboard user reading the message with the
  tab focus inside it is doing exactly what a pointer user hovering is doing,
  and the clock should stop for both.
-->
<div
  class="toast {tones[toast.tone]}"
  role="status"
  onmouseenter={() => onpause(toast.id)}
  onmouseleave={() => onresume(toast.id)}
  onfocusin={() => onpause(toast.id)}
  onfocusout={() => onresume(toast.id)}
>
  <p class="message">{toast.message()}</p>

  {#if toast.action}
    <button class="action" type="button" onclick={() => onact(toast.id)}>
      {toast.action.label()}
    </button>
  {/if}

  <IconButton label={dismissLabel} size="sm" onclick={() => ondismiss(toast.id)}>
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
      <path d="m6 6 12 12M18 6 6 18" stroke-linecap="round" />
    </svg>
  </IconButton>
</div>

<style>
  .toast {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    padding: var(--space-2) var(--space-2) var(--space-2) var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-md);
    background: var(--surface-overlay);
    box-shadow: var(--shadow-overlay);
    pointer-events: auto;
    animation: enter var(--duration-base) var(--ease-spatial);
  }

  /* The tone is a stripe on the leading edge, never the only signal: the
     message says what happened in words. */
  .success {
    border-inline-start: 3px solid var(--success);
  }

  .danger {
    border-inline-start: 3px solid var(--danger);
  }

  .message {
    flex: 1;
    min-width: 0;
    font-size: var(--text-sm);
  }

  .action {
    flex: none;
    min-height: var(--control-sm);
    padding-inline: var(--space-3);
    border: none;
    border-radius: var(--radius-md);
    background: transparent;
    color: var(--accent);
    font: inherit;
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
    cursor: pointer;
  }

  .action:hover {
    background: var(--surface-hover);
  }

  @keyframes enter {
    from {
      opacity: 0;
      transform: translateY(var(--space-2));
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .toast {
      animation: none;
    }
  }
</style>
