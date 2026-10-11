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
  }

  let { label, value, placeholder, onchange }: Props = $props();
</script>

<Field {label}>
  {#snippet children({ id, describedBy, invalid })}
    <div class="amount">
      <!-- Aria-hidden: the unit is in the section description; a lone symbol announced first is a
           word without a sentence. -->
      <span class="unit" aria-hidden="true">$</span>
      <TextInput
        {id}
        {describedBy}
        {invalid}
        inputmode="decimal"
        {placeholder}
        value={value?.toString() ?? ''}
        oninput={(text) => onchange(budget(text))}
      />
    </div>
  {/snippet}
</Field>

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
