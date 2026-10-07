<script lang="ts">
  import { onDestroy, onMount } from 'svelte';
  import type { AppError } from '$api';
  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { Button, Skeleton } from '$ds';
  import {
    readInvitation,
    redeemInvitation,
    type Redemption
  } from '$features/auth/households.svelte';
  import { session } from '$features/auth/session.svelte';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { createLoadingState } from '$shell/loadingState.svelte';

  /**
   * The page an invitation link opens; it works signed out and carries the code through sign-in or
   * sign-up.
   * The household name is not shown before signing in (the code is a bearer token), and a signed-in
   * user is asked before joining:
   * opening a link must not mean joining a stranger's household. The invitation is read, which uses
   * nothing up, to show whose it is.
   */
  const code = $derived(page.params.code ?? '');

  type Answer =
    | { readonly kind: 'member'; readonly household: Redemption }
    | { readonly kind: 'invalid' }
    | { readonly kind: 'failed'; readonly failure: AppError };

  let answer = $state<Answer | null>(null);
  let joining = $state(false);
  let householdName = $state<string | null>(null);
  let reading = $state(false);
  const loading = createLoadingState();

  const invalidCode = 'households.invitation_invalid';

  onMount(() => void open());
  onDestroy(() => loading.dispose());

  async function open() {
    await session.resolve();

    if (session.status !== 'authenticated') {
      return;
    }

    reading = true;
    loading.start();
    const read = await readInvitation(code);
    loading.stop();
    reading = false;

    if (read.ok) {
      householdName = read.value.householdName;
    } else if (read.error.code === invalidCode && answer === null && !joining) {
      answer = { kind: 'invalid' };
    }
  }

  async function join() {
    answer = null;
    joining = true;

    const outcome = await redeemInvitation(code);

    if (!('householdId' in outcome)) {
      joining = false;
      answer =
        outcome.code === invalidCode ? { kind: 'invalid' } : { kind: 'failed', failure: outcome };

      return;
    }

    if (outcome.alreadyMember) {
      answer = { kind: 'member', household: outcome };

      return;
    }

    // Re-read: the shell needs the household's name and role before it renders anything about it.
    session.reset();
    await session.resolve();
    await enter(outcome.householdId);
  }

  async function enter(householdId: string) {
    session.selectHousehold(householdId);
    await goto(resolve('/(app)'), { replaceState: true });
  }

  const registerHref = $derived(`${resolve('/(auth)/register')}?code=${encodeURIComponent(code)}`);

  const loginHref = $derived(
    `${resolve('/(auth)/login')}?next=${encodeURIComponent(resolve('/(auth)/join/[code]', { code }))}`
  );
</script>

<svelte:head><title>{m['auth.join.title']()}</title></svelte:head>

<div class="join">
  {#if answer?.kind === 'member'}
    {@const household = answer.household}
    <h1 class="title">{m['auth.join.member.title']({ household: household.name })}</h1>
    <p class="body">{m['auth.join.member.body']()}</p>

    <div class="actions">
      <Button variant="primary" size="lg" full onclick={() => void enter(household.householdId)}>
        {m['auth.join.open']({ household: household.name })}
      </Button>
    </div>
  {:else if answer?.kind === 'invalid' || answer?.kind === 'failed'}
    {#if answer.kind === 'invalid'}
      <h1 class="title">{m['auth.join.invalid.title']()}</h1>
      <p class="body">{m['auth.join.invalid.body']()}</p>
    {:else}
      <h1 class="title">{m['error.unexpected.title']()}</h1>
      <p class="body">{explain(answer.failure)}</p>
    {/if}

    <div class="actions">
      {#if answer.kind === 'failed'}
        <Button variant="primary" size="lg" full onclick={() => void join()}>
          {m['error.retry']()}
        </Button>
      {/if}
      <Button size="lg" full href={resolve('/(app)')}>{m['auth.join.home']()}</Button>
    </div>
  {:else if session.status === 'anonymous' || session.status === 'unavailable'}
    <h1 class="title">{m['auth.join.title']()}</h1>
    <p class="body">{m['welcome.body']()}</p>

    <div class="actions">
      <Button variant="primary" size="lg" full href={registerHref}>
        {m['auth.register.submit']()}
      </Button>

      <Button size="lg" full href={loginHref}>{m['auth.register.signIn']()}</Button>
    </div>
  {:else if session.status === 'authenticated'}
    <h1 class="title">{m['auth.join.title']()}</h1>
    <div class="invited" aria-busy={reading} aria-live="polite">
      {#if householdName}
        <p class="household">{m['auth.join.confirm.household']({ household: householdName })}</p>
      {:else if loading.showing}
        <Skeleton width="16rem" height="1.5em" />
      {/if}
    </div>
    <p class="body">{m['auth.join.confirm.body']()}</p>

    <div class="actions">
      <Button variant="primary" size="lg" full loading={joining} onclick={() => void join()}>
        {m['auth.join.confirm.submit']()}
      </Button>
      <Button size="lg" full href={resolve('/(app)')}>{m['auth.join.home']()}</Button>
    </div>
  {:else}
    <h1 class="title">{m['auth.join.title']()}</h1>
    <p class="body" role="status">{m['auth.join.joining']()}</p>
  {/if}
</div>

<style>
  .join {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .title {
    font-size: var(--text-2xl);
  }

  .body {
    color: var(--text-muted);
  }

  .household {
    font-size: var(--text-lg);
    font-weight: var(--weight-semibold);
  }

  .actions {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }
</style>
