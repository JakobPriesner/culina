<script lang="ts">
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { Button } from '$ds';
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

  const registerHref = $derived(`${resolve('/(auth)/register')}?code=${encodeURIComponent(code)}`);

  const loginHref = $derived(
    `${resolve('/(auth)/login')}?next=${encodeURIComponent(`/welcome?code=${code}`)}`
  );
</script>

<svelte:head><title>{m['auth.join.title']()}</title></svelte:head>

<div class="join">
  <h1 class="title">{m['auth.join.title']()}</h1>
  <p class="body">{m['welcome.body']()}</p>

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
