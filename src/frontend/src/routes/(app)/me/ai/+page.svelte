<script lang="ts">
  import { onMount } from 'svelte';

  import { Badge, Button, Disclosure, Field, Select, Switch, TextInput } from '$ds';
  import { explain } from '$shell/explain';
  import { formatNumber, m } from '$shell/i18n';

  import { assistance } from '$features/assistance/stores/assistance.svelte';
  import { session } from '$features/auth/session.svelte';
  import {
    capabilities,
    providerFacts,
    providers,
    providersFor,
    type Assistance,
    type Capability,
    type Connection,
    type Provider,
    type Use
  } from '$features/assistance/types';

  import SettingsRow from '../SettingsRow.svelte';
  import SettingsSection from '../SettingsSection.svelte';

  /**
   * Which models this instance can talk to, and which of them does what.
   *
   * Two lists rather than one form, because they answer different questions and
   * change at different rates. A provider is connected once; which provider does
   * a job is changed whenever somebody reads that a new model is better at it.
   * Flattening them into "the assistant's settings" is what made this a single
   * global choice — and a single global choice meant picking the provider that
   * was least bad at everything.
   *
   * The providers list shows every provider this build knows, connected or not,
   * so adding one is filling a row in rather than finding a button.
   *
   * The API key is the one control that is not a plain field, and it has to be.
   * No endpoint returns it, so there is nothing to put in a box — a box rendered
   * empty would read as "no key", and saving would then look like it had wiped
   * one.
   */

  let draft = $state<Assistance | null>(null);
  let saved = $state(false);

  onMount(async () => {
    await assistance.load();

    draft = assistance.settings ? structuredClone($state.snapshot(assistance.settings)) : null;
  });

  function editConnection(provider: Provider, patch: Partial<Connection>): void {
    if (!draft) return;

    draft.connections = draft.connections.map((one) =>
      one.provider === provider ? { ...one, ...patch } : one
    );
    saved = false;
  }

  function editUse(capability: Capability, patch: Partial<Use>): void {
    if (!draft) return;

    draft.uses = draft.uses.map((one) =>
      one.capability === capability ? { ...one, ...patch } : one
    );
    saved = false;
  }

  const connectionFor = (provider: Provider): Connection =>
    draft?.connections.find((one) => one.provider === provider) ?? {
      provider,
      apiKeyConfigured: false,
      baseUrl: '',
      usable: false
    };

  const useFor = (capability: Capability): Use =>
    draft?.uses.find((one) => one.capability === capability) ?? {
      capability,
      enabled: false,
      provider: '',
      model: '',
      defaultModel: ''
    };

  /** The providers this job could be given to, plus "not offered". */
  const choicesFor = (capability: Capability) => [
    { value: '', label: m['ai.job.none']() },
    ...providersFor(capability).map((provider) => ({
      value: provider,
      label: m[`ai.provider.${provider}`]()
    }))
  ];

  const offeredBy = (provider: Provider | '') =>
    provider === '' ? undefined : assistance.models.find((one) => one.provider === provider);

  /**
   * The models this job may be given, as the provider listed them.
   *
   * Filtered by what the job needs: drawing sees only the models that draw, and
   * the other three see only the ones that do not. An empty first entry keeps
   * "whatever Culina currently defaults to" reachable, which is what most
   * instances should stay on.
   *
   * The filter is a convenience and never a gate. Whether a model draws is the
   * adapter reading its name, because no provider states it — so when that
   * reading leaves a job with nothing to choose from, the whole catalogue is
   * offered instead of an empty list. Being wrong costs a call that fails with
   * a clear message; hiding the model somebody is paying for costs them the
   * feature, and no message at all explains where it went.
   *
   * Null means there is nothing to build a list from at all: the providers are
   * still being asked, this one did not answer, or it answered with an empty
   * catalogue. Those end in the text box, because a name can always be typed.
   */
  function modelsFor(capability: Capability, use: Use) {
    const listed = offeredBy(use.provider);

    if (!listed?.reachable || listed.models.length === 0) {
      return null;
    }

    const wanted = listed.models.filter((model) =>
      capability === 'draw' ? model.canDraw : !model.canDraw
    );

    const offered = wanted.length > 0 ? wanted : listed.models;

    return [
      { value: '', label: `${m['ai.job.model.any']()} — ${use.defaultModel}` },
      ...offered.map((model) => ({ value: model.id, label: model.label }))
    ];
  }

  /**
   * Whether a job is offered the provider's whole catalogue, because none of
   * it announced itself as the kind this job needs.
   *
   * Ordinary, and said under the picker so nobody wonders why a writing model
   * is offered to the job that draws: a provider can offer forty models and
   * none that draws.
   */
  function offersWholeCatalogue(capability: Capability, use: Use): boolean {
    const listed = offeredBy(use.provider);

    return (
      listed?.reachable === true &&
      listed.models.length > 0 &&
      !listed.models.some((model) => (capability === 'draw' ? model.canDraw : !model.canDraw))
    );
  }

  /**
   * What is wrong with a provider's list of models, said once, beside the
   * provider.
   *
   * It is a fact about the connection, not about any one job: a key that may
   * not read the catalogue leaves every job without a list. Said under each
   * job it was the same long paragraph three times, the page twice as long on
   * a phone, and a screen reader reading it three times over.
   */
  function catalogueProblem(provider: Provider): string | null {
    const listed = offeredBy(provider);
    const name = m[`ai.provider.${provider}`]();

    if (!listed) {
      return null;
    }

    // A refused key and a provider that is down read the same from here and
    // are not the same thing: one is replaced, the other is waited for.
    // "Check the key and the address" sent somebody to replace a key that
    // signs every other call in this app perfectly well — providers scope
    // keys, and reading the catalogue is a permission of its own.
    if (!listed.reachable) {
      return listed.problem === 'assistance.rejected'
        ? m['ai.models.rejected']({ provider: name })
        : m['ai.models.unreachable']({ provider: name });
    }

    return listed.models.length === 0 ? m['ai.models.emptyCatalogue']({ provider: name }) : null;
  }

  /** Whether a job's provider has no list for a reason already said above. */
  const unlistedByProvider = (use: Use): boolean =>
    use.provider !== '' && catalogueProblem(use.provider) !== null;

  function budget(value: string): number | null {
    const parsed = Number(value.replace(',', '.'));

    return value.trim().length === 0 || Number.isNaN(parsed) ? null : parsed;
  }

  async function save(): Promise<void> {
    if (!draft) return;

    const failure = await assistance.save($state.snapshot(draft) as Assistance);

    if (!failure) {
      draft = assistance.settings ? structuredClone($state.snapshot(assistance.settings)) : draft;
      saved = true;

      // What is set here is what decides whether the rest of the app shows an
      // assistant's buttons at all: the four switches travel with the signed-in
      // account, and that is read once when the app boots. Without this, giving
      // "improve a recipe" a provider left the editor with no button to press
      // until somebody reloaded the page — and nothing on screen said so.
      await session.refresh();
    }
  }

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

  /** Whether any job is given to a provider that sends data off the machine. */
  const anythingHosted = $derived(
    (draft?.uses ?? []).some(
      (use) => use.provider !== '' && providerFacts[use.provider].needsApiKey
    )
  );
</script>

<svelte:head><title>{m['me.ai']()}</title></svelte:head>

{#if draft}
  {@const it = draft}

  <SettingsSection title={m['ai.connections']()} description={m['ai.connections.hint']()}>
    {#each providers as provider (provider)}
      {@const facts = providerFacts[provider]}
      {@const connection = connectionFor(provider)}
      {@const problem = catalogueProblem(provider)}

      <SettingsRow
        label={m[`ai.provider.${provider}`]()}
        description={facts.needsApiKey ? m['ai.apiKey.hint']() : m['ai.address.required']()}
      >
        <Badge tone={connection.usable ? 'success' : 'neutral'}>
          {connection.usable ? m['ai.connected']() : m['ai.notConnected']()}
        </Badge>

        {#if !facts.needsApiKey}
          <!-- The address is the connection here, not an override of one. -->
          <Field label={m['ai.address']()}>
            {#snippet children({ id, describedBy, invalid })}
              <TextInput
                {id}
                {describedBy}
                {invalid}
                type="url"
                placeholder={facts.addressHint}
                value={connection.baseUrl}
                oninput={(value) => editConnection(provider, { baseUrl: value })}
              />
            {/snippet}
          </Field>
        {:else if connection.apiKey === undefined}
          <!-- No field, because there is nothing to show in one. -->
          <span class="state">
            {connection.apiKeyConfigured ? m['ai.apiKey.set']() : m['ai.apiKey.none']()}
          </span>
          <Button
            variant="secondary"
            size="sm"
            onclick={() => editConnection(provider, { apiKey: '' })}
          >
            {connection.apiKeyConfigured ? m['ai.apiKey.replace']() : m['ai.apiKey.add']()}
          </Button>
        {:else}
          <Field label={m['ai.apiKey']()}>
            {#snippet children({ id, describedBy, invalid })}
              <TextInput
                {id}
                {describedBy}
                {invalid}
                type="password"
                autocomplete="off"
                placeholder={m['ai.apiKey.placeholder']()}
                value={connection.apiKey ?? ''}
                oninput={(value) => editConnection(provider, { apiKey: value })}
              />
            {/snippet}
          </Field>
          <Button
            variant="ghost"
            size="sm"
            onclick={() => editConnection(provider, { apiKey: undefined })}
          >
            {m['ai.apiKey.cancel']()}
          </Button>
        {/if}

        {#if problem}
          <p class="failure" role="alert">{problem}</p>
        {/if}
      </SettingsRow>
    {/each}
  </SettingsSection>

  <!-- Said plainly, before anybody turns anything on. -->
  <p class="privacy">{anythingHosted ? m['ai.privacy.mixed']() : m['ai.privacy.local']()}</p>

  {#if assistance.unlisted}
    <!-- The listing itself did not happen, so there is no per-provider row to
         say so. Without this the pickers below are bare text boxes and the
         screen gives no reason for it. -->
    <p class="failure" role="alert">{m['ai.models.unlisted']()}</p>
  {/if}

  <SettingsSection title={m['ai.jobs']()} description={m['ai.jobs.hint']()}>
    {#each capabilities as capability (capability)}
      {@const use = useFor(capability)}

      {@const choices = modelsFor(capability, use)}

      <SettingsRow label={m[`ai.${capability}`]()} description={m[`ai.${capability}.hint`]()}>
        <Field label={m['ai.job.provider']()}>
          {#snippet children({ id, describedBy, invalid })}
            <Select
              {id}
              {describedBy}
              {invalid}
              inline
              value={use.provider}
              options={choicesFor(capability)}
              onchange={(value) =>
                editUse(capability, {
                  provider: value as Provider | '',
                  // Choosing a provider is switching the job on; choosing
                  // "not offered" is switching it off. One gesture, because
                  // there is no state where both answers are interesting.
                  enabled: value !== '',
                  // The old model belonged to the old provider. Carrying it
                  // over would name something the new one has never heard of.
                  model: ''
                })}
            />
          {/snippet}
        </Field>

        {#if use.provider !== ''}
          {#if !choices && assistance.listing}
            <!-- The provider has not answered yet. A placeholder select rather
                 than the text box below it: the box would be replaced by a
                 select a moment later, under whatever had been typed into it. -->
            <Field label={m['ai.job.model']()}>
              {#snippet children({ id, describedBy, invalid })}
                <Select
                  {id}
                  {describedBy}
                  {invalid}
                  inline
                  disabled
                  value=""
                  options={[{ value: '', label: m['ai.models.listing']() }]}
                />
              {/snippet}
            </Field>
          {:else if choices}
            <!-- The whole catalogue rather than the filtered part of it, when
                 the filter came up empty. Said plainly under the picker, so
                 nobody wonders why a writing model is being offered to the
                 job that draws. -->
            {@const unfiltered = offersWholeCatalogue(capability, use)}
            <Field
              label={m['ai.job.model']()}
              hint={unfiltered
                ? m['ai.models.unfiltered']({ provider: m[`ai.provider.${use.provider}`]() })
                : undefined}
            >
              {#snippet children({ id, describedBy, invalid })}
                <Select
                  {id}
                  {describedBy}
                  {invalid}
                  inline
                  value={use.model}
                  options={choices}
                  onchange={(value) => editUse(capability, { model: value })}
                />
              {/snippet}
            </Field>
          {:else}
            <!-- No list to choose from at all. A text box is what this was
                 before lists existed, and it still works — the convenience is
                 what is lost, never the ability to configure anything. -->
            <Field
              label={m['ai.job.model']()}
              hint={unlistedByProvider(use)
                ? m['ai.models.seeConnection']({ provider: m[`ai.provider.${use.provider}`]() })
                : undefined}
            >
              {#snippet children({ id, describedBy, invalid })}
                <TextInput
                  {id}
                  {describedBy}
                  {invalid}
                  placeholder={use.defaultModel || m['ai.models.typed']()}
                  value={use.model}
                  oninput={(value) => editUse(capability, { model: value })}
                />
              {/snippet}
            </Field>
          {/if}
        {/if}
      </SettingsRow>
    {/each}
  </SettingsSection>

  <SettingsSection bare>
    <Disclosure summary={m['ai.advanced']()}>
      <div class="advanced">
        <p class="note">{m['ai.advanced.hint']()}</p>

        {#each providers as provider (provider)}
          {#if providerFacts[provider].needsApiKey}
            <Field
              label={`${m[`ai.provider.${provider}`]()} — ${m['ai.address']()}`}
              hint={m['ai.address.optional']({ provider: m[`ai.provider.${provider}`]() })}
            >
              {#snippet children({ id, describedBy, invalid })}
                <TextInput
                  {id}
                  {describedBy}
                  {invalid}
                  type="url"
                  placeholder={providerFacts[provider].addressHint}
                  value={connectionFor(provider).baseUrl}
                  oninput={(value) => editConnection(provider, { baseUrl: value })}
                />
              {/snippet}
            </Field>
          {/if}
        {/each}
      </div>
    </Disclosure>
  </SettingsSection>

  <SettingsSection title={m['ai.budget']()} description={m['ai.budget.hint']()}>
    <SettingsRow label={m['ai.enabled']()} description={m['ai.enabled.hint']()}>
      <Switch
        checked={it.enabled}
        label={m['ai.enabled']()}
        onchange={(checked) => {
          it.enabled = checked;
          saved = false;
        }}
      />
    </SettingsRow>

    <SettingsRow label={m['ai.budget.monthly']()}>
      <Field label={m['ai.budget.monthly']()}>
        {#snippet children({ id, describedBy, invalid })}
          <div class="amount">
            <!-- Aria-hidden: the unit is in the section's own description, and
                 a lone currency symbol announced before the field would be a
                 word without a sentence. -->
            <span class="unit" aria-hidden="true">$</span>
            <TextInput
              {id}
              {describedBy}
              {invalid}
              inputmode="decimal"
              placeholder={m['ai.budget.none']()}
              value={it.monthlyBudget?.toString() ?? ''}
              oninput={(value) => {
                it.monthlyBudget = budget(value);
                saved = false;
              }}
            />
          </div>
        {/snippet}
      </Field>
    </SettingsRow>

    <SettingsRow label={m['ai.budget.personal']()}>
      <Field label={m['ai.budget.personal']()}>
        {#snippet children({ id, describedBy, invalid })}
          <div class="amount">
            <span class="unit" aria-hidden="true">$</span>
            <TextInput
              {id}
              {describedBy}
              {invalid}
              inputmode="decimal"
              placeholder={m['ai.budget.none']()}
              value={it.personalBudget?.toString() ?? ''}
              oninput={(value) => {
                it.personalBudget = budget(value);
                saved = false;
              }}
            />
          </div>
        {/snippet}
      </Field>
    </SettingsRow>
  </SettingsSection>

  <div class="actions">
    <Button onclick={save} loading={assistance.saving}>{m['ai.save']()}</Button>

    {#if saved}
      <span class="state" role="status">{m['ai.saved']()}</span>
    {/if}
  </div>

  {#if assistance.error}
    <p class="failure" role="alert">{explain(assistance.error)}</p>
  {/if}

  {#if assistance.usage}
    {@const usage = assistance.usage}

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
  {/if}
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

  /* Outside the enclosures, because it is a statement about the whole screen
     rather than a setting on it. */
  .privacy {
    max-width: var(--measure);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  .note {
    max-width: var(--measure);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  .failure {
    max-width: var(--measure);
    color: var(--text-danger);
    font-size: var(--text-sm);
  }

  .actions {
    display: flex;
    align-items: center;
    gap: var(--space-3);
  }

  /* The unit beside the field rather than inside it: typing "$" into a box
     that parses numbers is a mistake the box would have to reject. */
  .amount {
    display: flex;
    align-items: center;
    gap: var(--space-2);
  }

  .unit {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .advanced {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    padding-top: var(--space-3);
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
