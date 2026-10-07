<script lang="ts">
  import type { Snippet } from 'svelte';

  import Dialog from './Dialog.svelte';

  /**
   * A focused task that interrupts; prefer an undoable action with a toast over a confirmation
   * modal.
   */
  interface Props {
    open: boolean;
    title: string;
    wide?: boolean;
    children: Snippet;
    footer?: Snippet;
    closeLabel: string;
    onclose?: () => void;
  }

  let { open = $bindable(), ...rest }: Props = $props();
</script>

<Dialog bind:open placement="centre" {...rest} />
