<script lang="ts">
  import { onDestroy } from 'svelte';

  import { Button } from '$ds';
  import { formatDate, m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  import FormFailure from './FormFailure.svelte';
  import FormField from './FormField.svelte';
  import { issueRecoveryCode } from './recovery';
  import { createSubmission } from './submission.svelte';

  /** Helping somebody back into their account: the administrator reads out or sends the code, shown once and valid for a day (it passes through more hands). */
  let email = $state('');
  let issued = $state<{ email: string; code: string; expiresAt: string } | null>(null);

  const submission = createSubmission();

  onDestroy(() => submission.dispose());

  async function submit(event: SubmitEvent) {
    event.preventDefault();

    let made: typeof issued = null;

    const succeeded = await submission.run(async () => {
      const result = await issueRecoveryCode(email);

      if (!result.ok) {
        return result.error;
      }

      made = { email, ...result.value };

      return null;
    });

    if (succeeded) {
      issued = made;
      email = '';
    }
  }

  async function copy(code: string) {
    try {
      await navigator.clipboard.writeText(code);
      toaster.show({ message: () => m['me.invite.copied'](), tone: 'success' });
    } catch {
      // The code is on screen and can be selected.
    }
  }
</script>

<div class="panel">
  <form class="form" onsubmit={submit} novalidate>
    <FormFailure failure={submission.failure} />

    <FormField
      name="email"
      label={m['server.recovery.email']()}
      type="email"
      inputmode="email"
      autocomplete="off"
      bind:value={email}
      {submission}
    />

    <div>
      <Button type="submit" loading={submission.showingProgress}
        >{m['server.recovery.issue']()}</Button
      >
    </div>
  </form>

  {#if issued}
    <div class="fresh">
      <p class="label">{m['server.recovery.for']({ email: issued.email })}</p>
      <p class="code" data-testid="issued-recovery-code">{issued.code}</p>
      <p class="once">
        {m['server.recovery.once']({
          when: formatDate(new Date(issued.expiresAt), { dateStyle: 'medium', timeStyle: 'short' })
        })}
      </p>
      <Button onclick={() => copy(issued!.code)}>{m['server.recovery.copy']()}</Button>
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

  .fresh {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    align-items: flex-start;
    padding: var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
  }

  .label {
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
    overflow-wrap: anywhere;
  }

  .code {
    padding: var(--space-2) var(--space-3);
    border-radius: var(--radius-md);
    background: var(--surface-sunken);
    font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
    font-size: var(--text-lg);
    letter-spacing: 0.04em;
  }

  .once {
    font-size: var(--text-sm);
    color: var(--text-muted);
  }
</style>
