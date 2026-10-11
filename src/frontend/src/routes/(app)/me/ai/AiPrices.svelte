<script lang="ts">
  import type { AiSettingsDraft } from '$features/assistance/settings/createAiSettingsDraft.svelte';
  import { recordedOn } from '$features/assistance/settings/labels';
  import { halfPriced, pricedModels, type PricedModel } from '$features/assistance/settings/prices';
  import type { Price } from '$features/assistance/types';
  import { formatNumber, m } from '$shell/i18n';

  import SettingsRow from '../SettingsRow.svelte';
  import SettingsSection from '../SettingsSection.svelte';
  import DollarField from './DollarField.svelte';

  /**
   * The administrator's own prices for the billed models the jobs use. Culina's list goes stale and
   * only the administrator knows their rate; an empty box keeps Culina's price.
   */
  interface Props {
    settings: AiSettingsDraft;
  }

  let { settings }: Props = $props();

  const draft = $derived(settings.draft);
  const models = $derived(draft ? pricedModels(draft.uses) : []);

  const money = (value: number): string =>
    formatNumber(value, { style: 'currency', currency: 'USD', maximumFractionDigits: 3 });

  const unknown: Price = { inputPerMillion: null, outputPerMillion: null, perImage: null };

  /** Culina's number, as the placeholder of an empty box: what it will count at. */
  const placeholderFor = (value: number | null): string =>
    value === null ? m['ai.prices.unlisted']() : formatNumber(value);

  function culinaSays({ tokens, pictures, listed: price }: PricedModel): string {
    const { inputPerMillion: input, outputPerMillion: output, perImage } = price ?? unknown;
    const said: string[] = [];

    if (tokens && input !== null && output !== null) {
      said.push(m['ai.prices.listed.tokens']({ input: money(input), output: money(output) }));
    }

    if (pictures && perImage !== null) {
      said.push(m['ai.prices.listed.image']({ price: money(perImage) }));
    }

    return said.length > 0 ? said.join(' ') : m['ai.prices.listed.none']();
  }
</script>

{#if draft && models.length > 0}
  <SettingsSection
    title={m['ai.prices']()}
    description={m['ai.prices.hint']({ date: recordedOn(draft.pricesRecordedOn) })}
  >
    {#each models as model (model.model)}
      {@const price = settings.priceFor(model.model)}

      <SettingsRow label={model.model} description={culinaSays(model)}>
        <div class="prices">
          {#if model.tokens}
            <DollarField
              label={m['ai.prices.input']()}
              value={price.inputPerMillion}
              placeholder={placeholderFor(model.listed?.inputPerMillion ?? null)}
              onchange={(value) => settings.setPrice(model.model, { inputPerMillion: value })}
            />
            <DollarField
              label={m['ai.prices.output']()}
              value={price.outputPerMillion}
              placeholder={placeholderFor(model.listed?.outputPerMillion ?? null)}
              onchange={(value) => settings.setPrice(model.model, { outputPerMillion: value })}
            />
          {/if}

          {#if model.pictures}
            <DollarField
              label={m['ai.prices.image']()}
              value={price.perImage}
              placeholder={placeholderFor(model.listed?.perImage ?? null)}
              onchange={(value) => settings.setPrice(model.model, { perImage: value })}
            />
          {/if}
        </div>

        {#if model.tokens && halfPriced(price)}
          <p class="note">{m['ai.prices.half']()}</p>
        {/if}
      </SettingsRow>
    {/each}
  </SettingsSection>
{/if}

<style>
  .prices {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-3);
  }

  .note {
    max-width: var(--measure);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }
</style>
