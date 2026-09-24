<script lang="ts">
  import { onDestroy, onMount, untrack } from 'svelte';

  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { Button, Disclosure } from '$ds';
  import { m } from '$shell/i18n';

  import FormField from '$features/auth/FormField.svelte';
  import FormFailure from '$features/auth/FormFailure.svelte';
  import SubmitButton from '$features/auth/SubmitButton.svelte';
  import { register } from '$features/auth/registration.svelte';
  import { session } from '$features/auth/session.svelte';
  import { createSubmission } from '$features/auth/submission.svelte';
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

  /**
   * Setting up a fresh instance, in the order it has to happen.
   *
   * The database first, when there is none: nothing else can be saved until
   * there is somewhere to keep it. Then the two settings that depend on how the
   * server is put on the network — whether cookies need HTTPS, and which proxy
   * to believe — with everything else folded away, because the defaults are
   * right for a household. The account last, because the settings before it
   * restart the server, and turning secure cookies on or off renames the
   * session cookie: an account created first would be signed straight out.
   *
   * Whoever finishes this administers the instance. That is the same promise
   * the first registration always made; this only lets them set the server up
   * before making it.
   */
  let { data } = $props();

  type Step = 'database' | 'server' | 'account';

  // Fixed when the page opens: a step list that changed under somebody as they
  // went would make "step 2 of 3" mean two different things.
  const steps: readonly Step[] =
    untrack(() => data.setup?.stage) === 'database'
      ? ['database', 'server', 'account']
      : ['server', 'account'];

  let step = $state<Step>(steps[0]!);
  let databaseDraft = $state<DatabaseDraft | null>(null);
  let serverDraft = $state<ServerDraft | null>(null);
  let outcome = $state<SaveOutcome | null>(null);
  let saving = $state(false);

  let displayName = $state('');
  let email = $state('');
  let password = $state('');
  let householdName = $state('');

  const submission = createSubmission();

  onDestroy(() => submission.dispose());

  onMount(() => (step === 'database' ? openDatabase() : openServer()));

  async function openDatabase() {
    await server.loadDatabase();
    databaseDraft = server.database ? structuredClone($state.snapshot(server.database)) : null;
  }

  async function openServer() {
    await server.loadServer();

    if (!server.server || !server.serverFacts) return;

    const draft = structuredClone($state.snapshot(server.server));

    // The browser knows what the server cannot: whether this page came over
    // HTTPS. Behind a proxy that terminates TLS, every request the server
    // sees is plain HTTP, so its default has to be corrected from here.
    if (!server.serverFacts.pinned.has(variables.secure)) {
      draft.secure = location.protocol === 'https:';
    }

    serverDraft = draft;
  }

  /** Goes on once a save is in effect; stays, with the reason on screen, when it is not. */
  async function advance(result: SaveOutcome, next: () => Promise<void>) {
    outcome = result;

    if (result.kind === 'failed' || result.kind === 'stalled') {
      return;
    }

    // A database that already has an administrator — an existing Culina moved
    // to a new server — has nothing left to set up.
    if (result.kind === 'applied' && result.setup.stage === 'complete') {
      await goto(resolve('/(auth)/login'), { replaceState: true });

      return;
    }

    outcome = null;
    await next();
  }

  async function connect(event: SubmitEvent) {
    event.preventDefault();

    if (!databaseDraft || unreadableDatabaseNumbers(databaseDraft).length > 0) return;

    saving = true;
    const result = await server.saveDatabase($state.snapshot(databaseDraft) as DatabaseDraft);
    saving = false;

    await advance(result, async () => {
      step = 'server';
      await openServer();
    });
  }

  async function configure(event: SubmitEvent) {
    event.preventDefault();

    if (!serverDraft || unreadableNumbers(serverDraft).length > 0) return;

    saving = true;
    const result = await server.saveServer($state.snapshot(serverDraft) as ServerDraft);
    saving = false;

    await advance(result, async () => {
      step = 'account';
    });
  }

  async function retry() {
    await advance(await server.awaitRestart(), async () => {
      step = step === 'database' ? 'server' : 'account';

      if (step === 'server') await openServer();
    });
  }

  async function createAccount(event: SubmitEvent) {
    event.preventDefault();

    const succeeded = await submission.run(async () => {
      const outcome = await register({ email, displayName, password, householdName });

      // Registering signs you in; asking for the same password again on the
      // next screen would be a pointless second step.
      return 'code' in outcome ? outcome : session.signIn(email, password);
    });

    if (succeeded) {
      await goto(resolve('/(app)'), { replaceState: true });
    }
  }

  const busy = $derived(saving || server.phase !== 'idle');
  const position = $derived(steps.indexOf(step) + 1);
</script>

<svelte:head><title>{m['setup.title']()}</title></svelte:head>

<div class="setup">
  <header class="intro">
    <p class="eyebrow">{m['setup.step']({ current: position, total: steps.length })}</p>
    <h1 class="title">
      {#if step === 'database'}
        {m['setup.database.title']()}
      {:else if step === 'server'}
        {m['setup.server.title']()}
      {:else}
        {m['setup.account.title']()}
      {/if}
    </h1>
    <p class="subtitle">
      {#if step === 'database'}
        {m['setup.database.intro']()}
      {:else if step === 'server'}
        {m['setup.server.intro']()}
      {:else}
        {m['setup.account.intro']()}
      {/if}
    </p>
  </header>

  {#if step === 'database' && databaseDraft && server.databaseFacts}
    <form class="form" onsubmit={connect} novalidate>
      <DatabaseFields bind:draft={databaseDraft} facts={server.databaseFacts} disabled={busy} />

      <SaveStatus phase={server.phase} {outcome} onretry={retry} />

      <Button type="submit" variant="primary" loading={busy}>{m['setup.database.submit']()}</Button>
    </form>
  {:else if step === 'server' && serverDraft && server.serverFacts}
    {@const facts = server.serverFacts}

    <form class="form" onsubmit={configure} novalidate>
      <SecureCookiesField
        bind:checked={serverDraft.secure}
        pinned={facts.pinned.has(variables.secure)}
        disabled={busy}
      />

      <ProxyFields bind:draft={serverDraft} {facts} disabled={busy} />

      <Disclosure summary={m['setup.server.advanced']()}>
        <div class="advanced">
          <SessionFields bind:draft={serverDraft} {facts} disabled={busy} />
          <LimitFields bind:draft={serverDraft} {facts} disabled={busy} />
          <TelemetryFields bind:draft={serverDraft} {facts} disabled={busy} />
        </div>
      </Disclosure>

      <SaveStatus phase={server.phase} {outcome} onretry={retry} />

      <Button type="submit" variant="primary" loading={busy}>{m['setup.server.submit']()}</Button>
    </form>
  {:else if step === 'account'}
    <form class="form" onsubmit={createAccount} novalidate>
      <FormFailure failure={submission.failure} />

      <FormField
        name="displayName"
        label={m['auth.register.displayName']()}
        autocomplete="name"
        autofocus
        bind:value={displayName}
        {submission}
      />

      <FormField
        name="email"
        label={m['auth.register.email']()}
        type="email"
        autocomplete="username"
        inputmode="email"
        bind:value={email}
        {submission}
      />

      <FormField
        name="password"
        label={m['auth.register.password']()}
        hint={m['auth.register.passwordHint']()}
        type="password"
        autocomplete="new-password"
        bind:value={password}
        {submission}
      />

      <FormField
        name="householdName"
        label={m['auth.register.householdName']()}
        hint={m['auth.register.householdHint']()}
        required={false}
        bind:value={householdName}
        {submission}
      />

      <SubmitButton label={m['setup.account.submit']()} {submission} />
    </form>
  {:else if server.error}
    <p class="failure" role="alert">{m['setup.unreadable']()}</p>
    <Button onclick={() => (step === 'database' ? openDatabase() : openServer())}>
      {m['error.retry']()}
    </Button>
  {/if}
</div>

<style>
  .setup,
  .form,
  .advanced {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .advanced {
    padding-top: var(--space-3);
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-display);
    font-weight: var(--weight-regular);
    letter-spacing: -0.04em;
  }

  .intro {
    margin-bottom: var(--space-2);
  }

  .eyebrow {
    color: var(--accent);
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.12em;
    text-transform: uppercase;
    margin-bottom: var(--space-3);
  }

  .subtitle {
    margin-top: var(--space-3);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-relaxed);
  }

  .failure {
    color: var(--text-danger);
    font-size: var(--text-sm);
  }
</style>
