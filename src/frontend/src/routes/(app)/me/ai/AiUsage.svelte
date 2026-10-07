<script lang="ts">
  import type { Usage } from '$features/assistance/types';
  import { formatNumber, m } from '$shell/i18n';

  import SettingsRow from '../SettingsRow.svelte';
  import SettingsSection from '../SettingsSection.svelte';

  interface Props {
    usage: Usage;
  }

  let { usage }: Props = $props();

  /**
   * A sum of money, with its unit on it.
   *
   * Dollars, stated rather than implied: it is what the hosted providers bill
   * in, so it is what the prices these figures are built from are quoted in.
   * The alternative is a bare number beside another bare number, and nobody
   * can be sure those are the same kind of thing.
   */
  const money = (value: number): string =>
    formatNumber(value, { style: 'currency', currency: 'USD', maximumFractionDigits: 2 });
</script>

<SettingsSection title={m['ai.usage']()} description={m['ai.usage.hint']()}>
  <SettingsRow label={m['ai.usage.spent']()}>
    <span class="spent">
      {money(usage.totalCost)}
      {#if usage.monthlyBudget !== null}
        <span class="of">{m['ai.usage.of']({ budget: money(usage.monthlyBudget) })}</span>
      {/if}
    </span>
  </SettingsRow>

  <SettingsRow
    label={m['ai.usage.tokens']({
      input: formatNumber(usage.totalInputTokens),
      output: formatNumber(usage.totalOutputTokens)
    })}
  >
    <span class="state">{m['ai.usage.pictures']({ count: usage.totalPictures })}</span>
  </SettingsRow>
</SettingsSection>

{#if usage.byPerson.length === 0}
  <p class="note">{m['ai.usage.none']()}</p>
{:else}
  <!-- A table, not a chart. Six rows of numbers on a family instance are
       six rows of numbers; a dashboard would be decoration. -->
  <table>
    <thead>
      <tr>
        <th scope="col">{m['ai.usage.person']()}</th>
        <th scope="col" class="number">{m['ai.usage.calls']()}</th>
        <th scope="col" class="number">{m['ai.usage.cost']()}</th>
      </tr>
    </thead>
    <tbody>
      {#each usage.byPerson as person (person.userId)}
        <tr>
          <td>{person.displayName}</td>
          <td class="number">{formatNumber(person.calls)}</td>
          <td class="number">{money(person.cost)}</td>
        </tr>
      {/each}
    </tbody>
  </table>
{/if}

{#if usage.unpriced > 0}
  <p class="note">{m['ai.usage.unpriced']({ count: usage.unpriced })}</p>
{/if}

<style>
  .state {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .spent {
    font-size: var(--text-lg);
    font-weight: var(--weight-semibold);
  }

  .of {
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-weight: var(--weight-regular);
  }

  .note {
    max-width: var(--measure);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  table {
    width: 100%;
    border-collapse: collapse;
    font-size: var(--text-sm);
  }

  th,
  td {
    padding: var(--space-2) var(--space-3);
    border-bottom: 1px solid var(--border);
    text-align: left;
  }

  th {
    color: var(--text-subtle);
    font-weight: var(--weight-medium);
  }

  .number {
    text-align: right;
    font-variant-numeric: tabular-nums;
  }
</style>
