<script lang="ts">
  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { Button } from '$ds';
  import { redeemInvitation } from '$features/auth/households.svelte';
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';

  /**
   * The page an invitation link opens.
   *
   * It works signed out, which is the whole point of sending someone a link:
   * the code is carried through sign-in or sign-up and redeemed on the other
   * side, so nobody has to copy it out of a message and paste it into a form
   * they have not reached yet.
   *
   * The household's name is deliberately not shown before signing in. An
   * invitation code is a bearer token, and anyone holding the link would
   * otherwise learn what they had been handed the keys to.
   */
  const code = $derived(page.params.code ?? '');

  /**
   * Somebody already signed in is simply let in.
   *
   * An invitation arrives in a chat, and the person opening it is very often
   * already using Culina on that phone. Asking them to sign in again is how an
   * invitation stops being followed; sending them to the "you have no
   * household yet" screen when they have one is worse, because it is untrue.
   * So the code is redeemed here and they land in the kitchen they were asked
   * to join.
   */
  let refused = $state(false);

  $effect(() => {
    void session.resolve().then(async () => {
      if (session.status !== 'authenticated' || !code) {
        return;
      }

      const outcome = await redeemInvitation(code);

      if (typeof outcome !== 'string') {
        refused = true;

        return;
      }

      // Re-read: the household arrives with a name and a role, and the shell
      // needs both before it renders anything about it.
      session.reset();
      await session.resolve();
      await goto(resolve('/(app)'), { replaceState: true });
    });
  });

  const registerHref = $derived(`${resolve('/(auth)/register')}?code=${encodeURIComponent(code)}`);

  const loginHref = $derived(
    `${resolve('/(auth)/login')}?next=${encodeURIComponent(`/welcome?code=${code}`)}`
  );
</script>

<svelte:head><title>{m['auth.join.title']()}</title></svelte:head>

<div class="join">
  <h1 class="title">{refused ? m['auth.join.invalid.title']() : m['auth.join.title']()}</h1>
  <p class="body">{refused ? m['auth.join.invalid.body']() : m['welcome.body']()}</p>

  <div class="actions">
    <Button variant="primary" size="lg" full href={registerHref}>
      {m['auth.register.submit']()}
    </Button>

    <Button size="lg" full href={loginHref}>{m['auth.register.signIn']()}</Button>
  </div>
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
