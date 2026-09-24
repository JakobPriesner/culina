<script lang="ts">
  import { onMount } from 'svelte';

  import { invalidateAll } from '$app/navigation';
  import { Button, ErrorState, Skeleton } from '$ds';
  import { m } from '$shell/i18n';

  import { session } from '$features/auth/session.svelte';
  import DatabaseFields from '$features/server/DatabaseFields.svelte';
  import LimitFields from '$features/server/LimitFields.svelte';
  import ProxyFields from '$features/server/ProxyFields.svelte';
  import SaveStatus from '$features/server/SaveStatus.svelte';
  import SecureCookiesField from '$features/server/SecureCookiesField.svelte';
  import SessionFields from '$features/server/SessionFields.svelte';
  import TelemetryFields from '$features/server/TelemetryFields.svelte';
  import { server, type SaveOutcome } from '$features/server/stores/server.svelte';
  import {
    unreadableDatabaseNumbers,
    unreadableNumbers,
    variables,
    type DatabaseDraft,
    type ServerDraft
  } from '$features/server/types';

  import SettingsSection from '../SettingsSection.svelte';

  /**
   * How the server runs: its database, what it trusts, what it allows.
   *
   * Two saves rather than one, because they are two different kinds of risk.
   * The server settings take effect with a restart and can be put back from
   * this screen; the database is connected to before it is saved, and pointing
   * it somewhere new is the one change here that leaves the instance with
   * different data afterwards.
   *
   * Every save that changes something restarts the server, which is said on
   * the button rather than in a dialog: it takes a second or two, and a
   * confirmation for that would be a click spent on reassurance.
   */

  let serverDraft = $state<ServerDraft | null>(null);
  let databaseDraft = $state<DatabaseDraft | null>(null);
  let serverOutcome = $state<SaveOutcome | null>(null);
  let databaseOutcome = $state<SaveOutcome | null>(null);
  let saving = $state<'server' | 'database' | null>(null);

  function copyDrafts() {
    serverDraft = server.server ? structuredClone($state.snapshot(server.server)) : null;
    databaseDraft = server.database ? structuredClone($state.snapshot(server.database)) : null;
  }

  onMount(async () => {
    await server.load();
    copyDrafts();
  });

  /**
   * After a restart the session may not have survived it: turning secure
   * cookies on or off renames the cookie, and the browser then holds one the
   * new host does not look for. Asked rather than assumed, and sent to sign in
   * again — back to this page — only when it really is gone.
   */
  async function settle(outcome: SaveOutcome) {
    if (outcome.kind !== 'applied') {
      return;
    }

    await session.refresh();

    if (session.status !== 'authenticated') {
      // The app's own guard sends them to sign in, and back here afterwards.
      await invalidateAll();

      return;
    }

    await server.load();
    copyDrafts();
  }

  async function saveServer() {
    if (!serverDraft || unreadableNumbers(serverDraft).length > 0) return;

    saving = 'server';
    serverOutcome = await server.saveServer($state.snapshot(serverDraft) as ServerDraft);
    saving = null;

    await settle(serverOutcome);
  }

  async function saveDatabase() {
    if (!databaseDraft || unreadableDatabaseNumbers(databaseDraft).length > 0) return;

    saving = 'database';
    databaseOutcome = await server.saveDatabase($state.snapshot(databaseDraft) as DatabaseDraft);
    saving = null;

    await settle(databaseOutcome);
  }

  async function retry(which: 'server' | 'database') {
    const outcome = await server.awaitRestart();

    if (which === 'server') {
      serverOutcome = outcome;
    } else {
      databaseOutcome = outcome;
    }

    await settle(outcome);
  }

  const busy = $derived(saving !== null || server.phase !== 'idle');
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
      <Button variant="primary" onclick={() => server.load().then(copyDrafts)}>
        {m['error.retry']()}
      </Button>
    {/snippet}
  </ErrorState>
{:else if serverDraft && databaseDraft && server.serverFacts && server.databaseFacts}
  {@const facts = server.serverFacts}
  {@const databaseFacts = server.databaseFacts}
  {@const readonly = !facts.writable}

  {#if readonly}
    <p class="notice" role="note">{m['server.readonly']()}</p>
  {/if}

  <SettingsSection title={m['server.signIn']()} description={m['server.signIn.hint']()}>
    <div class="fields">
      <SecureCookiesField
        bind:checked={serverDraft.secure}
        pinned={facts.pinned.has(variables.secure)}
        disabled={readonly || busy}
      />
      <SessionFields bind:draft={serverDraft} {facts} disabled={readonly || busy} />
    </div>
  </SettingsSection>

  <SettingsSection title={m['server.proxy']()} description={m['server.proxy.hint']()}>
    <div class="fields">
      <ProxyFields bind:draft={serverDraft} {facts} disabled={readonly || busy} />
    </div>
  </SettingsSection>

  <SettingsSection title={m['server.limits']()} description={m['server.limits.hint']()}>
    <div class="fields">
      <LimitFields bind:draft={serverDraft} {facts} disabled={readonly || busy} />
    </div>
  </SettingsSection>

  <SettingsSection title={m['server.telemetry']()} description={m['server.telemetry.hint']()}>
    <div class="fields">
      <TelemetryFields bind:draft={serverDraft} {facts} disabled={readonly || busy} />
    </div>
  </SettingsSection>

  <div class="actions">
    <Button onclick={saveServer} loading={saving === 'server'} disabled={readonly || busy}>
      {m['server.save']()}
    </Button>
    <p class="note">{m['server.save.hint']()}</p>
  </div>
  <SaveStatus
    phase={saving === 'server' ? server.phase : 'idle'}
    outcome={serverOutcome}
    onretry={() => retry('server')}
  />

  <SettingsSection title={m['server.database']()} description={m['server.database.hint']()}>
    <div class="fields">
      <DatabaseFields
        bind:draft={databaseDraft}
        facts={databaseFacts}
        disabled={!databaseFacts.writable || busy}
      />
      <p class="note">{m['server.database.move']()}</p>
    </div>
  </SettingsSection>

  <div class="actions">
    <Button
      onclick={saveDatabase}
      loading={saving === 'database'}
      disabled={!databaseFacts.writable || busy}
    >
      {m['server.database.save']()}
    </Button>
  </div>
  <SaveStatus
    phase={saving === 'database' ? server.phase : 'idle'}
    outcome={databaseOutcome}
    onretry={() => retry('database')}
  />
{:else}
  <!-- The shape of the first two sections, so nothing jumps when they land. -->
  <div class="loading" aria-busy="true">
    {#each { length: 2 } as _, index (index)}
      <div class="placeholder" aria-hidden="true">
        <Skeleton width="10rem" height="1.25rem" />
        <Skeleton width="100%" height="9rem" shape="block" />
      </div>
    {/each}
  </div>
{/if}

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
