<script lang="ts">
  import TextArea from '../inputs/TextArea.svelte';

  interface Props {
    value?: string;
  }

  let { value = $bindable('') }: Props = $props();
</script>

<TextArea id="note" label="Note" bind:value />
