<script lang="ts">
  import type { Snippet } from 'svelte';

  import Dialog from './Dialog.svelte';

  /**
   * The modal's content placed where the hand is: bottom sheet on a phone, centred dialog on larger
   * screens, with one API so features can't pick the wrong one.
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
