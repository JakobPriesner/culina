<script lang="ts">
  /**
   * The navigation icons, in one place.
   *
   * Always outlined. These are open shapes — a book, a basket — and filling
   * one solid at 24px makes a blob rather than an emphasis. The current
   * destination is carried by the pill behind it, by colour, and by
   * `aria-current`, so there are three signals without a fourth that looks
   * worse.
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
  fill="none"
  stroke="currentColor"
  stroke-width={current ? 2 : 1.6}
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
  {:else if icon === 'cookbooks'}
    <!-- Books on a shelf, distinct from an open recipe. -->
    <rect x="3" y="4" width="4" height="16" rx="1" />
    <rect x="8" y="4" width="4" height="16" rx="1" />
    <path d="m15 5 4-1 3 15-4 1Z" />
  {:else if icon === 'plan'}
    <rect x="3" y="5" width="18" height="16" rx="3" />
    <path d="M7 3v4m10-4v4M3 11h18" />
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
