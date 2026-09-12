<script lang="ts">
  import type { Snippet } from 'svelte';

  import Dialog from './Dialog.svelte';

  /**
   * The same content as a modal, placed where the hand is.
   *
   * Rises from the bottom edge on a phone and becomes a centred dialog on a
   * larger screen — one component and one API, because two components would
   * drift and a feature would end up picking the wrong one for the viewport.
   */
  interface Props {
    open: boolean;
    title: string;
    children: Snippet;
    footer?: Snippet;
    closeLabel: string;
    hideTitle?: boolean;
    onclose?: () => void;
  }

  let { open = $bindable(), ...rest }: Props = $props();
</script>

<Dialog bind:open placement="sheet" {...rest} />
