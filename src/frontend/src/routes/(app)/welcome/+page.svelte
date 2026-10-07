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
  import DeletedHouseholdList from '$features/auth/DeletedHouseholdList.svelte';
  import {
    createHousehold,
    deletedHouseholds,
    redeemInvitation,
    type DeletedHousehold
  } from '$features/auth/households.svelte';
  import { session } from '$features/auth/session.svelte';
  import { createSubmission } from '$features/auth/submission.svelte';
  import { m } from '$shell/i18n';
  import Olli from '$shell/olli/Olli.svelte';

  /** An account with nowhere to cook: an instance allowing invitation-free accounts, or an owner who deleted their only household. Never a dead end. */
  // An invitation link arrives with its code, so the second tab opens prefilled.
  const invited = page.url.searchParams.get('code') ?? '';

  let tab = $state(invited ? 'join' : 'create');
  let name = $state('');
  let code = $state(invited);

  const submission = createSubmission();

  onDestroy(() => submission.dispose());

  let deleted = $state<readonly DeletedHousehold[]>([]);

  $effect(() => {
    void deletedHouseholds().then((result) => {
      deleted = result.ok ? result.value : [];
    });
  });

  async function join(attempt: () => Promise<string | AppError>) {
    const succeeded = await submission.run(async () => {
      const outcome = await attempt();

      if (typeof outcome !== 'string') {
        return outcome;
      }

      // Re-read rather than patching the store: the household's role and name would otherwise have a second source of truth.
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
  <Olli pose="hello" />
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
            void join(async () => {
              const outcome = await redeemInvitation(code);

              return 'householdId' in outcome ? outcome.householdId : outcome;
            });
          }}
          novalidate
        >
          <FormField name="code" label={m['welcome.joinTitle']()} bind:value={code} {submission} />
          <SubmitButton label={m['welcome.join']()} {submission} />
        </form>
      {/if}
    {/snippet}
  </Tabs>

  <!-- Where an owner who deleted their only household arrives, so the way back must be here too; nothing for everybody else. -->
  {#if deleted.length > 0}
    <section class="deleted">
      <h2 class="heading">{m['household.deleted.title']()}</h2>
      <p class="body">{m['household.deleted.body']()}</p>
      <DeletedHouseholdList items={deleted} />
    </section>
  {/if}
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

  .deleted {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    margin-top: var(--space-6);
  }

  .heading {
    font-size: var(--text-lg);
  }

  .form {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }
</style>
