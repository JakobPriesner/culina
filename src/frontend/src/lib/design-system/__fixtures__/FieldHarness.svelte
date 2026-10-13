<script lang="ts">
  import Field from '../inputs/Field.svelte';
  import RadioGroup from '../inputs/RadioGroup.svelte';
  import TextInput from '../inputs/TextInput.svelte';

  /** Exercises the wiring `Field` is supposed to produce by construction. */
  interface Props {
    label?: string;
    hint?: string;
    error?: string;
    group?: boolean;
  }

  let { label = 'Title', hint, error, group = false }: Props = $props();

  let value = $state('');
</script>

<Field {label} {hint} {error} {group}>
  {#snippet children({ id, describedBy, invalid })}
    {#if group}
      <RadioGroup
        name="kind"
        bind:value
        {describedBy}
        options={[
          { value: 'a', label: 'First' },
          { value: 'b', label: 'Second' }
        ]}
      />
    {:else}
      <TextInput {id} {describedBy} {invalid} bind:value />
    {/if}
  {/snippet}
</Field>
