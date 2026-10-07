<script lang="ts">
  import { navigating } from '$app/state';
  import { m } from '$shell/i18n';
</script>

{#if navigating.to}
  <span class="progress" role="progressbar" aria-label={m['app.navigating']()}></span>
{/if}

<style>
  /* A thin line across the top while a route resolves. Not a spinner: this is
     usually over before it is noticed, and a spinner that flashes is worse than
     nothing. */
  .progress {
    position: absolute;
    inset-block-start: 0;
    inset-inline: 0;
    height: 2px;
    background: var(--accent);
    box-shadow: 0 0 0.5rem var(--accent);
    transform-origin: left center;
    /* Held back for the first 150 ms so a quick navigation shows nothing, then
       a trickle that slows the further it gets: fast enough to read as
       progress, never claiming to be finished before the page is. */
    animation:
      reveal var(--duration-base) var(--ease-out) 150ms both,
      advance 8s cubic-bezier(0.1, 0.7, 0.2, 1) 150ms both;
  }

  @keyframes reveal {
    from {
      opacity: 0;
    }
  }

  @keyframes advance {
    0% {
      transform: scaleX(0);
    }
    10% {
      transform: scaleX(0.35);
    }
    40% {
      transform: scaleX(0.7);
    }
    100% {
      transform: scaleX(0.94);
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .progress {
      animation: none;
      transform: scaleX(0.5);
    }
  }
</style>
