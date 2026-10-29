<script lang="ts">
  import { onMount } from 'svelte';

  import { refreshAll } from '$app/navigation';
  import { Button, ErrorState, Skeleton } from '$ds';
  import { m } from '$shell/i18n';

  import { session } from '$features/auth/session.svelte';
  import DatabaseFields from '$features/instance/DatabaseFields.svelte';
  import LimitFields from '$features/instance/LimitFields.svelte';
  import ProxyFields from '$features/instance/ProxyFields.svelte';
  import SaveStatus from '$features/instance/SaveStatus.svelte';
  import SecureCookiesField from '$features/instance/SecureCookiesField.svelte';
  import SessionFields from '$features/instance/SessionFields.svelte';
  import ProductLookupFields from '$features/instance/ProductLookupFields.svelte';
  import PushRelayFields from '$features/instance/PushRelayFields.svelte';
  import RegistrationFields from '$features/instance/RegistrationFields.svelte';
  import TelemetryFields from '$features/instance/TelemetryFields.svelte';
  import { server, type SaveOutcome } from '$features/instance/stores/instance.svelte';
  import { variables } from '$features/instance/types';
  import { useServerDrafts, type ServerPart } from '$features/instance/useServerDrafts.svelte';

  import IssueRecoveryCodePanel from '$features/auth/IssueRecoveryCodePanel.svelte';

  import SettingsSection from '../SettingsSection.svelte';

  /**
   * How the server runs: its database, what it trusts, what it allows. Two saves for two kinds of risk: server settings
   * restart and can be put back here; the database is connected to before saving, and a new one leaves different data.
   * A restart is said on the button, not in a dialog.
   */

  const drafts = useServerDrafts();

  onMount(() => drafts.loadAll());

  /** After a restart the session may be gone (toggling secure cookies renames the cookie); checked, not assumed, and sent to sign in again only if lost. */
  async function settle(outcome: SaveOutcome) {
    if (outcome.kind !== 'applied') {
      return;
    }

    await session.refresh();

    if (session.status !== 'authenticated') {
      await refreshAll();

      return;
    }

    await drafts.loadAll();
  }

  async function saveServer() {
    const outcome = await drafts.saveServer();

    if (outcome) await settle(outcome);
  }

  async function saveDatabase() {
    const outcome = await drafts.saveDatabase();

    if (outcome) await settle(outcome);
  }

  async function retry(part: ServerPart) {
    await settle(await drafts.retry(part));
  }
</script>

<svelte:head><title>{m['me.server']()}</title></svelte:head>

{#if server.status === 'failed'}
  <ErrorState
    title={m['server.failed.title']()}
    body={m['server.failed.body']()}
    requestIdLabel={m['error.reference']()}
    requestId={server.error?.requestId}
  >
    {#snippet action()}
      <Button variant="primary" onclick={() => drafts.loadAll()}>
        {m['error.retry']()}
      </Button>
    {/snippet}
  </ErrorState>
{:else if drafts.serverDraft && drafts.databaseDraft && server.serverFacts && server.databaseFacts}
  {@const facts = server.serverFacts}
  {@const databaseFacts = server.databaseFacts}
  {@const readonly = !facts.writable}

  {#if readonly}
    <p class="notice" role="note">{m['server.readonly']()}</p>
  {/if}

  <SettingsSection title={m['server.signIn']()} description={m['server.signIn.hint']()}>
    <div class="fields">
      <SecureCookiesField
        bind:checked={drafts.serverDraft.secure}
        pinned={facts.pinned.has(variables.secure)}
        disabled={readonly || drafts.busy}
      />
      <SessionFields bind:draft={drafts.serverDraft} {facts} disabled={readonly || drafts.busy} />
    </div>
  </SettingsSection>

  <SettingsSection title={m['server.proxy']()} description={m['server.proxy.hint']()}>
    <div class="fields">
      <ProxyFields bind:draft={drafts.serverDraft} {facts} disabled={readonly || drafts.busy} />
    </div>
  </SettingsSection>

  <SettingsSection title={m['server.limits']()} description={m['server.limits.hint']()}>
    <div class="fields">
      <LimitFields bind:draft={drafts.serverDraft} {facts} disabled={readonly || drafts.busy} />
    </div>
  </SettingsSection>

  <SettingsSection title={m['server.telemetry']()} description={m['server.telemetry.hint']()}>
    <div class="fields">
      <TelemetryFields bind:draft={drafts.serverDraft} {facts} disabled={readonly || drafts.busy} />
    </div>
  </SettingsSection>

  <SettingsSection title={m['server.push']()} description={m['server.push.hint']()}>
    <div class="fields">
      <PushRelayFields bind:draft={drafts.serverDraft} {facts} disabled={readonly || drafts.busy} />
    </div>
  </SettingsSection>

  <div class="actions">
    <Button
      onclick={saveServer}
      loading={drafts.saving === 'server'}
      disabled={readonly || drafts.busy}
    >
      {m['server.save']()}
    </Button>
    <p class="note">{m['server.save.hint']()}</p>
  </div>
  <SaveStatus
    phase={drafts.saving === 'server' ? server.phase : 'idle'}
    outcome={drafts.serverOutcome}
    onretry={() => retry('server')}
  />

  <SettingsSection title={m['server.database']()} description={m['server.database.hint']()}>
    <div class="fields">
      <DatabaseFields
        bind:draft={drafts.databaseDraft}
        facts={databaseFacts}
        disabled={!databaseFacts.writable || drafts.busy}
      />
      <p class="note">{m['server.database.move']()}</p>
    </div>
  </SettingsSection>

  <div class="actions">
    <Button
      onclick={saveDatabase}
      loading={drafts.saving === 'database'}
      disabled={!databaseFacts.writable || drafts.busy}
    >
      {m['server.database.save']()}
    </Button>
  </div>
  <SaveStatus
    phase={drafts.saving === 'database' ? server.phase : 'idle'}
    outcome={drafts.databaseOutcome}
    onretry={() => retry('database')}
  />
{:else}
  <div class="loading" aria-busy="true">
    {#each { length: 2 } as _, index (index)}
      <div class="placeholder" aria-hidden="true">
        <Skeleton width="10rem" height="1.25rem" />
        <Skeleton width="100%" height="9rem" shape="block" />
      </div>
    {/each}
  </div>
{/if}

<!-- Outside the settings above: saved at once, with no restart. -->
<SettingsSection title={m['server.registration']()} description={m['server.registration.hint']()}>
  <RegistrationFields />
</SettingsSection>

<!-- Outside the settings above: saved at once, with no restart. -->
<SettingsSection title={m['server.products']()} description={m['server.products.hint']()}>
  <ProductLookupFields />
</SettingsSection>

<!-- Outside the settings above: needs no restart and must work while the configuration cannot be read. -->
<SettingsSection title={m['server.recovery']()} description={m['server.recovery.hint']()} panel>
  <IssueRecoveryCodePanel />
</SettingsSection>

<style>
  .fields {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    padding: var(--space-4);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2) var(--space-4);
  }

  .note,
  .notice {
    max-width: var(--measure);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  .notice {
    padding: var(--space-3) var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
    color: var(--text);
  }

  .loading,
  .placeholder {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .loading {
    gap: var(--layout-section-gap);
  }
</style>
