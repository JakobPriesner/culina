<script lang="ts">
  import Button from '../actions/Button.svelte';
  import Modal from '../overlay/Modal.svelte';
  import TextInput from '../inputs/TextInput.svelte';

  /** A trigger, a dialog with two focusables, and something outside it. */
  interface Props {
    onclose?: () => void;
  }

  let { onclose }: Props = $props();

  let open = $state(false);
  let value = $state('');
</script>

<Button onclick={() => (open = true)}>Open</Button>
<Button>Outside</Button>

<Modal bind:open title="Rename recipe" closeLabel="Close" {onclose}>
  <TextInput id="rename" bind:value />

  {#snippet footer()}
    <Button variant="primary" onclick={() => (open = false)}>Save</Button>
  {/snippet}
</Modal>
