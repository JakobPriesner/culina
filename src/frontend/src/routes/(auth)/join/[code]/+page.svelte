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
   * The page an invitation link opens.
   *
   * It works signed out, which is the whole point of sending someone a link:
   * the code is carried through sign-in or sign-up and redeemed on the other
   * side, so nobody has to copy it out of a message and paste it into a form
   * they have not reached yet. Signing in comes back here, so the code is
   * redeemed by the one page that knows how to answer every outcome.
   *
   * The household's name is deliberately not shown before signing in. An
   * invitation code is a bearer token, and anyone holding the link would
   * otherwise learn what they had been handed the keys to.
   *
   * Somebody already signed in is never offered a sign-in or an account they
   * already have, but is asked before the code is redeemed. Opening a link must
   * not be the same as joining: anybody can make a household and send its
   * link, and a person let straight in would be switched into a stranger's
   * kitchen, adding their recipes and lists there and showing them their name.
   * The owner checking the link before sending it is told they are already in,
   * and taken back to their kitchen.
   *
   * Asking is only fair if the question says whose kitchen it is, so once the
   * session is known to be signed in the invitation is read — which uses
   * nothing up — and the household's name is shown above the Join button. A
   * code that no longer works says so straight away; any other failure leaves
   * the question as it was, without a name, and pressing Join answers it.
   */
  const code = $derived(page.params.code ?? '');

  type Answer =
    | { readonly kind: 'member'; readonly household: Redemption }
    | { readonly kind: 'invalid' }
    | { readonly kind: 'failed'; readonly failure: AppError };

  let answer = $state<Answer | null>(null);
  let joining = $state(false);
  /** Whose kitchen the link is for, once read; null until then or if it could not be. */
  let householdName = $state<string | null>(null);
  let reading = $state(false);
  const loading = createLoadingState();

  const invalidCode = 'households.invitation_invalid';

  onMount(() => void open());
  onDestroy(() => loading.dispose());

  /** Resolves the session, and reads the invitation for somebody signed in. */
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

    // Re-read: the household arrives with a name and a role, and the shell
    // needs both before it renders anything about it.
    session.reset();
    await session.resolve();
    await enter(outcome.householdId);
  }

  /** Into the kitchen the link was for, rather than whichever was open last. */
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
