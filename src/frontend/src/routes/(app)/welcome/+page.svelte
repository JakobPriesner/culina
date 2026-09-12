<script lang="ts">
  import { onDestroy } from 'svelte';

  import type { AppError } from '$api';
  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { Tabs } from '$ds';
  import FormField from '$features/auth/FormField.svelte';
  import FormFailure from '$features/auth/FormFailure.svelte';
  import SubmitButton from '$features/auth/SubmitButton.svelte';
  import { createHousehold, redeemInvitation } from '$features/auth/households.svelte';
  import { session } from '$features/auth/session.svelte';
  import { createSubmission } from '$features/auth/submission.svelte';
  import { m } from '$shell/i18n';

  /**
   * An account with nowhere to cook.
   *
   * Reachable in one case: an instance that allows accounts without an
   * invitation. It is never a dead end — both ways out are on the screen, and
   * neither is hidden behind the other.
   */
  // An invitation link lands here with its code already in hand, so the second
  // tab opens with the field filled and nothing to copy out of a message.
  const invited = page.url.searchParams.get('code') ?? '';

  let tab = $state(invited ? 'join' : 'create');
  let name = $state('');
  let code = $state(invited);

  const submission = createSubmission();

  onDestroy(() => submission.dispose());

  async function join(attempt: () => Promise<string | AppError>) {
    const succeeded = await submission.run(async () => {
      const outcome = await attempt();

      if (typeof outcome !== 'string') {
        return outcome;
      }

      // Re-read rather than patching the store: the household arrives with a
      // role and a name, and inventing them here would be a second source of
      // truth for the same facts.
      await session.refresh();

      return null;
    });

    if (succeeded) {
      await goto(resolve('/(app)'), { replaceState: true });
    }
  }
</script>

<svelte:head><title>{m['welcome.title']()}</title></svelte:head>

<div class="page">
  <h1 class="title">{m['welcome.title']()}</h1>
  <p class="body">{m['welcome.body']()}</p>

  <FormFailure failure={submission.failure} />

  <Tabs
    bind:selected={tab}
    label={m['welcome.title']()}
    tabs={[
      { id: 'create', label: m['welcome.create']() },
      { id: 'join', label: m['welcome.join']() }
    ]}
  >
    {#snippet children(selected)}
      {#if selected === 'create'}
        <form
          class="form"
          onsubmit={(event) => {
            event.preventDefault();
            void join(() => createHousehold(name));
          }}
          novalidate
        >
          <FormField
            name="name"
            label={m['welcome.createTitle']()}
            bind:value={name}
            {submission}
          />
          <SubmitButton label={m['welcome.create']()} {submission} />
        </form>
      {:else}
        <form
          class="form"
          onsubmit={(event) => {
            event.preventDefault();
            void join(() => redeemInvitation(code));
          }}
          novalidate
        >
          <FormField name="code" label={m['welcome.joinTitle']()} bind:value={code} {submission} />
          <SubmitButton label={m['welcome.join']()} {submission} />
        </form>
      {/if}
    {/snippet}
  </Tabs>
</div>

<style>
  .page {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    max-width: 28rem;
    margin-inline: auto;
    padding: var(--space-12) var(--space-4);
  }

  .title {
    font-size: var(--text-2xl);
  }

  .body {
    color: var(--text-muted);
  }

  .form {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }
</style>
