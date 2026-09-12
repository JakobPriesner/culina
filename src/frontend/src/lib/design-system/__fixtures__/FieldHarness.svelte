<script lang="ts">
  import Field from '../inputs/Field.svelte';
  import TextInput from '../inputs/TextInput.svelte';

  /** Exercises the wiring `Field` is supposed to produce by construction. */
  interface Props {
    label?: string;
    hint?: string;
    error?: string;
  }

  let { label = 'Title', hint, error }: Props = $props();

  let value = $state('');
</script>

<Field {label} {hint} {error}>
  {#snippet children({ id, describedBy, invalid })}
    <TextInput {id} {describedBy} {invalid} bind:value />
  {/snippet}
</Field>
