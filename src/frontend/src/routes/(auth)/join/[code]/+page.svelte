<script lang="ts">
  import type { AppError } from '$api';
  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { Button } from '$ds';
  import { redeemInvitation, type Redemption } from '$features/auth/households.svelte';
  import { session } from '$features/auth/session.svelte';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';

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
   * Somebody already signed in is simply let in — and never offered a sign-in
   * or an account they already have. The person opening a link is very often
   * already using Culina on that phone, and quite often it is the owner,
   * checking the link before sending it: they are told they are already in,
   * and taken back to their kitchen.
   */
  const code = $derived(page.params.code ?? '');

  type Answer =
    | { readonly kind: 'member'; readonly household: Redemption }
    | { readonly kind: 'invalid' }
    | { readonly kind: 'failed'; readonly failure: AppError };

  let answer = $state<Answer | null>(null);

  const invalidCode = 'households.invitation_invalid';

  $effect(() => {
    void session.resolve().then(() => {
      if (session.status === 'authenticated' && code) {
        void redeem(code);
      }
    });
  });

  async function redeem(invitation: string) {
    answer = null;

    const outcome = await redeemInvitation(invitation);

    if (!('householdId' in outcome)) {
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
        <Button variant="primary" size="lg" full onclick={() => void redeem(code)}>
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

  .actions {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }
</style>
