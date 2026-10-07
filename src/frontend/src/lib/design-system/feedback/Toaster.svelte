<script lang="ts">
  import { toaster } from '$shell/toaster.svelte';

  import Toast from './Toast.svelte';

  /**
   * Where messages appear; mounted once in the app shell. Each toast has `role="status"` (polite, never `assertive`: interrupting a screen reader to announce a success is rude);
   * the role sits on the toast, not the strip, so an inserted message is announced once.
   */
  interface Props {
    /** Names the region, so it can be found in a landmarks list. */
    label: string;
    dismissLabel: string;
  }

  let { label, dismissLabel }: Props = $props();
</script>

<div class="toaster" role="region" aria-label={label}>
  {#each toaster.toasts as toast (toast.id)}
    <Toast
      {toast}
      {dismissLabel}
      onact={(id) => toaster.act(id)}
      ondismiss={(id) => toaster.dismiss(id)}
      onpause={(id) => toaster.pause(id)}
      onresume={(id) => toaster.resume(id)}
    />
  {/each}
</div>

<style>
  .toaster {
    position: fixed;
    z-index: var(--z-toast);
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    /* Above whatever the shell parks at the bottom (cooking bar, bottom nav, or neither) and the home indicator. */
    inset-block-end: calc(var(--bottom-inset) + var(--space-4) + env(safe-area-inset-bottom, 0px));
    inset-inline: var(--space-4);
    /* The strip itself must not intercept taps; each toast opts back in. */
    pointer-events: none;
  }

  /* A peek must not cover the message already sitting above this one. */
  .toaster :global(.toast:not(:first-child) .art) {
    display: none;
  }

  @media (min-width: 48rem) {
    .toaster {
      inset-inline: auto var(--space-6);
      max-width: 24rem;
    }
  }
</style>
