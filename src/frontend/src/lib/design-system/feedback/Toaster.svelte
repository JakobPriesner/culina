<script lang="ts">
  import { toaster } from '$shell/toaster.svelte';

  import Toast from './Toast.svelte';

  /**
   * Where the messages appear. Mounted once, in the app shell.
   *
   * Each toast carries `role="status"`, which is a polite live region — never
   * `assertive`: a toast reports something that already happened, and
   * interrupting whatever a screen reader was reading to announce a success is
   * rude in exactly the way the live-region spec warns about. The role sits on
   * the toast rather than the strip so an inserted message is announced once.
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
    /* Above the bottom navigation on a phone, and clear of the home indicator. */
    inset-block-end: calc(var(--space-4) + env(safe-area-inset-bottom, 0px));
    inset-inline: var(--space-4);
    /* The strip itself must not intercept taps; each toast opts back in. */
    pointer-events: none;
  }

  @media (min-width: 48rem) {
    .toaster {
      inset-inline: auto var(--space-6);
      max-width: 24rem;
    }
  }
</style>
