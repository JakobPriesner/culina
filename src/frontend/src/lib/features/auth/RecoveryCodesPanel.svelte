<script lang="ts">
  import { onDestroy } from 'svelte';

  import { Button } from '$ds';
  import { ErrorCodes } from '$api';
  import { formatDate, m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  import FormFailure from './FormFailure.svelte';
  import FormField from './FormField.svelte';
  import { createRecoveryCodes, readRecoveryCodes } from './recovery';
  import { createSubmission } from './submission.svelte';

  /**
   * The way back in that does not depend on anybody else.
   *
   * On an instance with one account there is no administrator to ask, so
   * these ten codes are the only thing between a forgotten password and a lost
   * recipe book. The page says so, shows the codes once, and offers them as a
   * file — a list on a screen is gone the moment the tab closes, and the whole
   * point is to have them on the day this screen cannot be reached.
   */
  let remaining = $state<number | null>(null);
  let createdAt = $state<string | null>(null);
  let fresh = $state<string[] | null>(null);
  let asking = $state(false);
  let password = $state('');

  const submission = createSubmission();

  onDestroy(() => submission.dispose());

  $effect(() => {
    void load();
  });

  async function load() {
    const result = await readRecoveryCodes();

    if (result.ok) {
      remaining = result.value.remaining;
      createdAt = result.value.createdAt ?? null;
    }
  }

  async function submit(event: SubmitEvent) {
    event.preventDefault();

    let codes: string[] = [];

    const succeeded = await submission.run(async () => {
      const result = await createRecoveryCodes(password);

      if (!result.ok) {
        return result.error;
      }

      codes = [...result.value.codes];
      createdAt = result.value.createdAt;

      return null;
    });

    if (!succeeded) {
      return;
    }

    fresh = codes;
    remaining = codes.length;
    password = '';
    asking = false;
  }

  /** The codes as a plain text file, which is what survives a closed tab. */
  function download(codes: readonly string[]) {
    const text = [m['me.recovery.fileHeading']({ origin: location.origin }), '', ...codes, ''].join(
      '\n'
    );
    const url = URL.createObjectURL(new Blob([text], { type: 'text/plain' }));
    const link = Object.assign(document.createElement('a'), {
      href: url,
      download: 'culina-recovery-codes.txt'
    });

    link.click();
    URL.revokeObjectURL(url);
  }

  async function copy(codes: readonly string[]) {
    try {
      await navigator.clipboard.writeText(codes.join('\n'));
      toaster.show({ message: () => m['me.invite.copied'](), tone: 'success' });
    } catch {
      // The codes are on screen and can be selected.
    }
  }
</script>

<div class="panel">
  {#if remaining !== null}
    <p class="status" data-testid="recovery-status">
      {#if remaining === 0}
        {m['me.recovery.none']()}
      {:else}
        {m['me.recovery.remaining']({
          count: remaining,
          when: createdAt ? formatDate(new Date(createdAt), { dateStyle: 'long' }) : ''
        })}
      {/if}
    </p>
  {/if}

  {#if fresh}
    <div class="fresh">
      <p class="label">{m['me.recovery.fresh']()}</p>
      <!-- Readable and selectable, not fields: nothing here is to be typed. -->
      <ul class="codes" data-testid="recovery-codes">
        {#each fresh as code (code)}
          <li>{code}</li>
        {/each}
      </ul>
      <p class="once">{m['me.recovery.once']()}</p>
      <div class="actions">
        <Button variant="primary" onclick={() => download(fresh!)}
          >{m['me.recovery.download']()}</Button
        >
        <Button onclick={() => copy(fresh!)}>{m['me.recovery.copy']()}</Button>
      </div>
    </div>
  {/if}

  {#if asking}
    <form class="form" onsubmit={submit} novalidate>
      <FormFailure
        failure={submission.failure}
        message={submission.failure?.code === ErrorCodes.incorrectPassword
          ? m['me.password.incorrect']()
          : undefined}
      />

      <FormField
        name="password"
        label={m['me.password.current']()}
        hint={remaining ? m['me.recovery.replaces']() : undefined}
        type="password"
        autocomplete="current-password"
        autofocus
        bind:value={password}
        {submission}
      />

      <div class="actions">
        <Button type="submit" variant="primary" loading={submission.showingProgress}>
          {m['me.recovery.create']()}
        </Button>
        <Button variant="ghost" onclick={() => (asking = false)}>{m['me.recovery.cancel']()}</Button
        >
      </div>
    </form>
  {:else}
    <div>
      <Button variant={remaining ? 'secondary' : 'primary'} onclick={() => (asking = true)}>
        {remaining ? m['me.recovery.recreate']() : m['me.recovery.create']()}
      </Button>
    </div>
  {/if}
</div>

<style>
  .panel {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    min-width: 0;
  }

  .status {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .form {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    max-width: 28rem;
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  /* Raised, like a fresh invitation: the one thing on the page that cannot be
     read again is the one thing that is impossible to scroll past. */
  .fresh {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    padding: var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
  }

  .label {
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
  }

  /* Columns where there is room, monospaced so a 0 and a 1 are never in doubt
     when copied by hand. Unnumbered: the codes have no order, and a "10." took
     the room the tenth code needed. */
  .codes {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(12rem, 1fr));
    gap: var(--space-1) var(--space-4);
    margin: 0;
    padding: var(--space-3);
    border-radius: var(--radius-md);
    background: var(--surface-sunken);
    list-style: none;
    font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
    font-size: var(--text-sm);
  }

  .once {
    font-size: var(--text-sm);
    color: var(--text-muted);
  }
</style>
