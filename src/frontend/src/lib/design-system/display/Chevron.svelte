<script lang="ts">
  /** The one way the app draws "back" and "on": a chevron, never a text arrow, so every link that leads somewhere looks the same. Decorative; its link says where. */
  interface Props {
    direction?: 'back' | 'forward';
  }

  let { direction = 'forward' }: Props = $props();
</script>

<svg
  class="chevron"
  class:back={direction === 'back'}
  viewBox="0 0 24 24"
  fill="none"
  stroke="currentColor"
  stroke-width="2"
  stroke-linecap="round"
  stroke-linejoin="round"
  aria-hidden="true"
>
  <path d="m9 6 6 6-6 6" />
</svg>

<style>
  .chevron {
    display: inline-block;
    flex: none;
    width: 1em;
    height: 1em;
    vertical-align: -0.125em;
  }

  .back {
    transform: scaleX(-1);
  }

  /* Back points against the reading direction, whichever way that runs. */
  :global([dir='rtl']) .chevron {
    transform: scaleX(-1);
  }

  :global([dir='rtl']) .back {
    transform: none;
  }
</style>
