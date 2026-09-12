<script lang="ts">
  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { Button, Divider } from '$ds';
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';
  import LocalePicker from '$shell/LocalePicker.svelte';
  import ThemeToggle from '$shell/ThemeToggle.svelte';

  /**
   * Everything about this person and this device.
   *
   * Appearance and language live here rather than in the top bar of every page:
   * they are set once and then never again, and a control that is used twice a
   * year does not belong where the eye lands every time.
   */
  async function signOut() {
    await session.signOut();
    await goto(resolve('/(auth)/login'), { replaceState: true });
  }
</script>

<svelte:head><title>{m['me.title']()}</title></svelte:head>

<div class="page">
  <h1>{m['me.title']()}</h1>

  {#if session.user}
    <p class="who">{session.user.displayName}</p>
    <p class="email">{session.user.email}</p>
  {/if}

  <Divider />

  <section class="section">
    <h2>{m['me.appearance']()}</h2>
    <div class="setting">
      <span class="setting-label">{m['me.theme']()}</span>
      <ThemeToggle />
    </div>

    <LocalePicker />
  </section>

  {#if session.activeHousehold}
    <Divider />

    <section class="section">
      <h2>{m['me.household']()}</h2>
      <p>{session.activeHousehold.name}</p>
    </section>
  {/if}

  <Divider />

  <Button onclick={signOut}>{m['auth.signOut']()}</Button>
</div>

<style>
  .page {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
    max-width: var(--measure);
    margin: 0 auto;
    padding: var(--space-6) var(--space-4) var(--space-16);
    align-items: flex-start;
  }

  .who {
    font-weight: var(--weight-medium);
  }

  .email {
    color: var(--text-muted);
  }

  .section {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  h2 {
    font-size: var(--text-lg);
  }

  .setting {
    display: flex;
    align-items: center;
    gap: var(--space-2);
  }

  .setting-label {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
