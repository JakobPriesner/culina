<script lang="ts">
  import { onMount } from 'svelte';

  import { Button, Disclosure, Field, SegmentedControl, Switch, TextInput } from '$ds';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { formatNumber } from '$shell/i18n';

  import { assistance } from '$features/assistance/stores/assistance.svelte';
  import { providerFacts, type Assistance, type Provider } from '$features/assistance/types';

  import SettingsRow from '../SettingsRow.svelte';
  import SettingsSection from '../SettingsSection.svelte';

  /**
   * Connecting a model, and seeing what it has cost.
   *
   * The first screen in Culina that configures the *instance* rather than the
   * person looking at it, which is why the category is only in the rail for an
   * administrator. It sits under `/me` anyway rather than in an admin area of
   * its own: there is exactly one such screen, and inventing a second top-level
   * section of the app to hold one page would be furniture with nothing in it.
   *
   * The API key is the one control here that is not a plain field, and it has to
   * be. No endpoint returns it, so there is nothing to put in a box — a box that
   * rendered empty would read as "no key", and saving the form would then look
   * like it had wiped one. So the row states whether a key is set and offers to
   * replace it, and only then is there a field at all.
   *
   * Everything is one form with one Save. Settings screens that save on every
   * keystroke are right for a theme and wrong for this: a half-typed model name
   * is a broken assistant, and a key pasted in two goes would be sent to the
   * server in two goes.
   */

  let draft = $state<Assistance | null>(null);

  /**
   * Three states, matching what the endpoint distinguishes: `undefined` leaves
   * the stored key alone, `''` removes it, and a value replaces it.
   */
  let apiKey = $state<string | undefined>(undefined);
  let saved = $state(false);

  const facts = $derived(draft ? providerFacts[draft.provider] : null);

  const segments = $derived([
    { id: 'gemini', label: m['ai.provider.gemini']() },
    { id: 'openai', label: m['ai.provider.openai']() },
    { id: 'ollama', label: m['ai.provider.ollama']() }
  ]);

  onMount(async () => {
    await assistance.load();

    draft = assistance.settings ? { ...assistance.settings } : null;
  });

  function edit(patch: Partial<Assistance>): void {
    if (draft) {
      draft = { ...draft, ...patch };
      saved = false;
    }
  }

  /**
   * Switching provider carries the models over but not their meaning.
   *
   * A model name is provider-specific, so keeping `gpt-4o-mini` selected after
   * switching to Ollama would be a setting that is silently wrong. Cleared, so
   * the placeholder shows what a sensible name looks like for the new one.
   */
  function chooseProvider(id: string): void {
    const provider = id as Provider;

    edit({
      provider,
      composeModel: '',
      drawModel: '',
      drawEnabled: providerFacts[provider].canDraw ? draft?.drawEnabled : false
    });
  }

  function budget(value: string): number | null {
    const parsed = Number(value.replace(',', '.'));

    return value.trim().length === 0 || Number.isNaN(parsed) ? null : parsed;
  }

  async function save(): Promise<void> {
    if (!draft) return;

    const failure = await assistance.save(draft, apiKey);

    if (!failure) {
      draft = assistance.settings ? { ...assistance.settings } : draft;
      apiKey = undefined;
      saved = true;
    }
  }

  const money = (value: number): string => formatNumber(value, { maximumFractionDigits: 2 });
</script>

<svelte:head><title>{m['me.ai']()}</title></svelte:head>

{#if draft && facts}
  <!-- Bound once, because a snippet is its own function and the narrowing
       from the `{#if}` above does not reach inside one. -->
  {@const it = draft}
  <SettingsSection title={m['ai.connection']()} description={m['ai.connection.hint']()}>
    <SettingsRow label={m['ai.provider']()} group>
      <SegmentedControl
        {segments}
        selected={it.provider}
        label={m['ai.provider']()}
        onselect={chooseProvider}
      />
    </SettingsRow>

    {#if facts.needsApiKey}
      <SettingsRow label={m['ai.apiKey']()} description={m['ai.apiKey.hint']()}>
        {#if apiKey === undefined}
          <!-- No field, because there is nothing to show in one. -->
          <span class="state">
            {it.apiKeyConfigured ? m['ai.apiKey.set']() : m['ai.apiKey.none']()}
          </span>
          <Button variant="secondary" size="sm" onclick={() => (apiKey = '')}>
            {m['ai.apiKey.replace']()}
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
                bind:value={() => apiKey ?? '', (value) => (apiKey = value)}
              />
            {/snippet}
          </Field>
          <Button variant="ghost" size="sm" onclick={() => (apiKey = undefined)}>
            {m['ai.apiKey.cancel']()}
          </Button>
        {/if}
      </SettingsRow>
    {/if}

    {#if facts.needsAddress}
      <!-- Only where it is the connection rather than an override of one. A
           model on your own machine is wherever you put it; Google's and
           OpenAI's addresses are not a deployment decision, and asking for
           them made connecting look like more work than it is. -->
      <SettingsRow label={m['ai.address']()} description={m['ai.address.required']()}>
        <Field label={m['ai.address']()}>
          {#snippet children({ id, describedBy, invalid })}
            <TextInput
              {id}
              {describedBy}
              {invalid}
              type="url"
              placeholder={facts.addressHint}
              value={it.baseUrl}
              oninput={(value) => edit({ baseUrl: value })}
            />
          {/snippet}
        </Field>
      </SettingsRow>
    {/if}

    <SettingsRow label={m['ai.enabled']()} description={m['ai.enabled.hint']()}>
      <Switch
        checked={it.enabled}
        label={m['ai.enabled']()}
        onchange={(checked) => edit({ enabled: checked })}
      />
    </SettingsRow>
  </SettingsSection>

  <!-- Said plainly, before anybody turns anything on, because it is the one
       thing about this feature somebody might not expect. -->
  <p class="privacy">
    {facts.needsApiKey
      ? m['ai.privacy.hosted']({ provider: m[`ai.provider.${it.provider}`]() })
      : m['ai.privacy.local']()}
  </p>

  <!-- No section heading: the disclosure already says the word, and a title
       above a summary saying the same thing is one of them too many. -->
  <SettingsSection bare>
    <Disclosure summary={m['ai.advanced']()}>
      <div class="advanced">
        <p class="note">{m['ai.advanced.hint']()}</p>

        {#if !facts.needsAddress}
          <Field
            label={m['ai.address']()}
            hint={m['ai.address.optional']({ provider: m[`ai.provider.${it.provider}`]() })}
          >
            {#snippet children({ id, describedBy, invalid })}
              <TextInput
                {id}
                {describedBy}
                {invalid}
                type="url"
                placeholder={facts.addressHint}
                value={it.baseUrl}
                oninput={(value) => edit({ baseUrl: value })}
              />
            {/snippet}
          </Field>
        {/if}

        <!-- Empty means the default for this provider, which the placeholder
             shows. Pinning a model is a real need and an uncommon one. -->
        <Field label={m['ai.composeModel']()} hint={m['ai.models.default']()}>
          {#snippet children({ id, describedBy, invalid })}
            <TextInput
              {id}
              {describedBy}
              {invalid}
              placeholder={facts.composeHint}
              value={it.composeModel}
              oninput={(value) => edit({ composeModel: value })}
            />
          {/snippet}
        </Field>

        {#if facts.canDraw}
          <Field label={m['ai.drawModel']()} hint={m['ai.models.default']()}>
            {#snippet children({ id, describedBy, invalid })}
              <TextInput
                {id}
                {describedBy}
                {invalid}
                placeholder={facts.drawHint}
                value={it.drawModel}
                oninput={(value) => edit({ drawModel: value })}
              />
            {/snippet}
          </Field>
        {/if}
      </div>
    </Disclosure>
  </SettingsSection>

  <SettingsSection title={m['ai.capabilities']()} description={m['ai.capabilities.hint']()}>
    <SettingsRow label={m['ai.improve']()} description={m['ai.improve.hint']()}>
      <Switch
        checked={it.improveEnabled}
        label={m['ai.improve']()}
        onchange={(checked) => edit({ improveEnabled: checked })}
      />
    </SettingsRow>

    <SettingsRow label={m['ai.draft']()} description={m['ai.draft.hint']()}>
      <Switch
        checked={it.draftEnabled}
        label={m['ai.draft']()}
        onchange={(checked) => edit({ draftEnabled: checked })}
      />
    </SettingsRow>

    <SettingsRow label={m['ai.read']()} description={m['ai.read.hint']()}>
      <Switch
        checked={it.readEnabled}
        label={m['ai.read']()}
        onchange={(checked) => edit({ readEnabled: checked })}
      />
    </SettingsRow>

    <!-- Absent rather than disabled for a provider that cannot draw: a switch
         that exists and can never be turned on is a question with one answer. -->
    {#if facts.canDraw}
      <SettingsRow label={m['ai.draw']()} description={m['ai.draw.hint']()}>
        <Switch
          checked={it.drawEnabled}
          label={m['ai.draw']()}
          onchange={(checked) => edit({ drawEnabled: checked })}
        />
      </SettingsRow>
    {/if}
  </SettingsSection>

  <SettingsSection
    title={m['ai.budget']()}
    description={facts.needsApiKey ? m['ai.budget.hint']() : m['ai.budget.free']()}
  >
    <SettingsRow label={m['ai.budget.monthly']()}>
      <Field label={m['ai.budget.monthly']()}>
        {#snippet children({ id, describedBy, invalid })}
          <TextInput
            {id}
            {describedBy}
            {invalid}
            inputmode="decimal"
            placeholder={m['ai.budget.none']()}
            value={it.monthlyBudget?.toString() ?? ''}
            oninput={(value) => edit({ monthlyBudget: budget(value) })}
          />
        {/snippet}
      </Field>
    </SettingsRow>

    <SettingsRow label={m['ai.budget.personal']()}>
      <Field label={m['ai.budget.personal']()}>
        {#snippet children({ id, describedBy, invalid })}
          <TextInput
            {id}
            {describedBy}
            {invalid}
            inputmode="decimal"
            placeholder={m['ai.budget.none']()}
            value={it.personalBudget?.toString() ?? ''}
            oninput={(value) => edit({ personalBudget: budget(value) })}
          />
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
      <p class="quiet">{m['ai.usage.none']()}</p>
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
      <p class="quiet">{m['ai.usage.unpriced']({ count: usage.unpriced })}</p>
    {/if}
  {/if}
{/if}

<style>
  .advanced {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    padding-top: var(--space-3);
  }

  .note {
    max-width: var(--measure);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

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

  .quiet {
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
