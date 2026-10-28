<script lang="ts">
  import { m } from '$shell/i18n';

  import SettingsRow from '../SettingsRow.svelte';
  import DollarField from './DollarField.svelte';

  /** One limit in dollars, blank for none. */
  interface Props {
    label: string;
    value: number | null;
    onchange: (value: number | null) => void;
  }

  let { label, value, onchange }: Props = $props();
</script>

<SettingsRow {label}>
  <DollarField unlabelled {label} {value} placeholder={m['ai.budget.none']()} {onchange} />
</SettingsRow>
