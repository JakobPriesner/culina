<script lang="ts">
  import type { Snippet } from 'svelte';

  import Dialog from './Dialog.svelte';

  /**
   * A moment that has the screen to itself (a welcome, a walk-through): the whole phone, a large
   * centred panel elsewhere. Rare by design; a task belongs in a `Modal` or `Sheet`.
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

<Dialog bind:open placement="stage" {...rest} />
