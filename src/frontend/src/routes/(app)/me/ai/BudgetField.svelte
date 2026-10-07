<script lang="ts">
  import { Field, TextInput } from '$ds';
  import { budget } from '$features/assistance/settings/modelChoices';
  import { m } from '$shell/i18n';

  import SettingsRow from '../SettingsRow.svelte';

  /** One limit in dollars, blank for none. */
  interface Props {
    label: string;
    value: number | null;
    onchange: (value: number | null) => void;
  }

  let { label, value, onchange }: Props = $props();
</script>

<SettingsRow {label}>
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
          placeholder={m['ai.budget.none']()}
          value={value?.toString() ?? ''}
          oninput={(text) => onchange(budget(text))}
        />
      </div>
    {/snippet}
  </Field>
</SettingsRow>

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
