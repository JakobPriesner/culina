<script lang="ts">
  import type { Snippet } from 'svelte';

  import Dialog from './Dialog.svelte';

  /**
   * A focused task that interrupts what the person was doing.
   *
   * Reserve it for that. Culina prefers an undoable action with a toast over a
   * confirmation dialog: a modal to confirm something reversible costs everyone
   * a decision to protect against a mistake that was already cheap to fix.
   */
  interface Props {
    open: boolean;
    title: string;
    children: Snippet;
    footer?: Snippet;
    closeLabel: string;
    onclose?: () => void;
  }

  let { open = $bindable(), ...rest }: Props = $props();
</script>

<Dialog bind:open placement="centre" {...rest} />
