<script lang="ts">
  /**
   * The three navigation icons, in one place.
   *
   * Outlined at rest and filled when current, which is the second signal
   * alongside colour — colour alone fails for the people most likely to be
   * cooking one-handed in bad light.
   */
  import type { DestinationIcon } from './navigation';

  interface Props {
    icon: DestinationIcon;
    current: boolean;
  }

  let { icon, current }: Props = $props();
</script>

<svg
  viewBox="0 0 24 24"
  fill={current ? 'currentColor' : 'none'}
  stroke="currentColor"
  stroke-width="1.6"
  stroke-linecap="round"
  stroke-linejoin="round"
>
  {#if icon === 'recipes'}
    <!-- A book, open. -->
    <path
      d="M4 5.5A1.5 1.5 0 0 1 5.5 4H10a2 2 0 0 1 2 2v13a2 2 0 0 0-2-2H5.5A1.5 1.5 0 0 1 4 15.5Z"
    />
    <path
      d="M20 5.5A1.5 1.5 0 0 0 18.5 4H14a2 2 0 0 0-2 2v13a2 2 0 0 1 2-2h4.5a1.5 1.5 0 0 0 1.5-1.5Z"
    />
  {:else if icon === 'shopping'}
    <!-- A basket. -->
    <path d="M4 8h16l-1.4 10a2 2 0 0 1-2 1.7H7.4a2 2 0 0 1-2-1.7Z" />
    <path d="M9 8 12 3l3 5" fill="none" />
  {:else}
    <!-- A person. -->
    <circle cx="12" cy="8.5" r="3.5" />
    <path d="M5 20a7 7 0 0 1 14 0" />
  {/if}
</svg>

<style>
  svg {
    display: block;
    width: 100%;
    height: 100%;
  }
</style>
