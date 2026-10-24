<script lang="ts">
  import { onDestroy } from 'svelte';

  import { ErrorCodes } from '$api';
  import { Button } from '$ds';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  import { claudeCommand, connectAgent, devices, mcpUrl } from './devices.svelte';
  import FormFailure from './FormFailure.svelte';
  import FormField from './FormField.svelte';
  import { session } from './session.svelte';
  import { createSubmission } from './submission.svelte';

  /**
   * A token for an AI agent (Claude Code, or anything that speaks MCP), made with the password and shown once.
   * Held in this component only: it is gone with "Done" or when the page is left, and nothing writes it anywhere.
   */
  let asking = $state(false);
  let password = $state('');
  let token = $state<string | null>(null);

  const url = mcpUrl();
  const submission = createSubmission();

  onDestroy(() => submission.dispose());

  async function submit(event: SubmitEvent) {
    event.preventDefault();

    const email = session.user?.email;

    if (!email) {
      return;
    }

    let made: string | null = null;

    const succeeded = await submission.run(async () => {
      const result = await connectAgent(email, password);

      if (!result.ok) {
        return result.error;
      }

      made = result.value;

      return null;
    });

    if (!succeeded) {
      return;
    }

    token = made;
    password = '';
    asking = false;
    // The new agent belongs in the list of devices straight away, so it can be signed out again.
    void devices.load();
  }

  function cancel() {
    asking = false;
    password = '';
    submission.clear();
  }

  async function copy(text: string) {
    try {
      await navigator.clipboard.writeText(text);
      toaster.show({ message: () => m['me.invite.copied'](), tone: 'success' });
    } catch {
      // A refused clipboard isn't worth a message: the text is on screen and can be selected.
    }
  }
</script>

{#snippet copyable(label: string, text: string, copyLabel: string)}
  <div class="copyable">
    <p class="label">{label}</p>
    <!-- Readable and selectable, not an input, which would invite editing a credential. -->
    <code class="value">{text}</code>
    <div>
      <Button size="sm" onclick={() => copy(text)}>{copyLabel}</Button>
    </div>
  </div>
{/snippet}

<div class="panel">
  {#if token}
    <div class="fresh">
      {@render copyable(m['me.agent.token'](), token, m['me.agent.copyToken']())}
      <p class="once" role="status">{m['me.agent.once']()}</p>
      {@render copyable(m['me.agent.url'](), url, m['me.agent.copyUrl']())}
      {@render copyable(
        m['me.agent.command'](),
        claudeCommand(url, token),
        m['me.agent.copyCommand']()
      )}
      <div>
        <Button variant="primary" onclick={() => (token = null)}>{m['me.agent.done']()}</Button>
      </div>
    </div>
  {:else if asking}
    <form class="form" onsubmit={submit} novalidate>
      <FormFailure
        failure={submission.failure}
        message={submission.failure?.code === ErrorCodes.invalidCredentials
          ? m['me.password.incorrect']()
          : undefined}
      />

      <FormField
        name="password"
        label={m['me.password.current']()}
        hint={m['me.agent.passwordHint']()}
        type="password"
        autocomplete="current-password"
        autofocus
        bind:value={password}
        {submission}
      />

      <div class="actions">
        <Button type="submit" variant="primary" loading={submission.showingProgress}>
          {m['me.agent.create']()}
        </Button>
        <Button variant="ghost" onclick={cancel}>{m['me.agent.cancel']()}</Button>
      </div>
    </form>
  {:else}
    <div>
      <Button onclick={() => (asking = true)}>{m['me.agent.connect']()}</Button>
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

  /* Raised like fresh recovery codes: the one unrepeatable thing is impossible to scroll past. */
  .fresh {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    padding: var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
  }

  .copyable {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    min-width: 0;
  }

  .label {
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
  }

  /* Monospaced so l and 1 are never in doubt; a token has no spaces to break at. */
  .value {
    display: block;
    padding: var(--space-3);
    border-radius: var(--radius-md);
    background: var(--surface-sunken);
    font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
    font-size: var(--text-sm);
    overflow-wrap: anywhere;
  }

  .once {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
