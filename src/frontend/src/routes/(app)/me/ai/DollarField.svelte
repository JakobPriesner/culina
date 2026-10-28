<script lang="ts">
  import { Field, TextInput } from '$ds';
  import { budget } from '$features/assistance/settings/modelChoices';

  /** An amount in US dollars, blank for none; providers bill and quote in dollars. */
  interface Props {
    label: string;
    value: number | null;
    /** What an empty box means. */
    placeholder: string;
    onchange: (value: number | null) => void;
    /** For a row that already prints the label: it names the box without a second visible copy. */
    unlabelled?: boolean;
  }

  let { label, value, placeholder, onchange, unlabelled = false }: Props = $props();

  const inputId = $props.id();
</script>

{#snippet amount(id: string, describedBy: string | undefined, invalid: boolean)}
  <div class="amount">
    <!-- Aria-hidden: the unit is in the section description; a lone symbol announced first is a
         word without a sentence. -->
    <span class="unit" aria-hidden="true">$</span>
    <TextInput
      {id}
      {describedBy}
      {invalid}
      label={unlabelled ? label : undefined}
      inputmode="decimal"
      {placeholder}
      value={value?.toString() ?? ''}
      oninput={(text) => onchange(budget(text))}
    />
  </div>
{/snippet}

{#if unlabelled}
  {@render amount(inputId, undefined, false)}
{:else}
  <Field {label}>
    {#snippet children({ id, describedBy, invalid })}
      {@render amount(id, describedBy, invalid)}
    {/snippet}
  </Field>
{/if}

<style>
  /* Unit beside the field: typing "$" into a number box would have to be rejected. */
  .amount {
    display: flex;
    align-items: center;
    gap: var(--space-2);
  }

  .unit {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
