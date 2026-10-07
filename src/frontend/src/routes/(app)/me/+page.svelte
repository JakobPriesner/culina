<script lang="ts">
  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { Avatar, Badge, Button } from '$ds';

  import { session } from '$features/auth/session.svelte';
  import { formatDate, m } from '$shell/i18n';
  import { update } from '$shell/updates.svelte';

  import SettingsRow from './SettingsRow.svelte';
  import SettingsSection from './SettingsSection.svelte';

  /** Who is signed in and the way out; deliberately the emptiest category. */
  const user = $derived(session.user);

  async function signOut() {
    await session.signOut();
    await goto(resolve('/(auth)/login'), { replaceState: true });
  }
</script>

<svelte:head><title>{m['me.account']()}</title></svelte:head>

<!-- Where the update offer waits after its toast is gone. -->
{#if update.ready}
  <SettingsSection>
    <SettingsRow label={m['me.update']()} description={m['app.update.available']()}>
      <Button variant="primary" onclick={() => update.apply()}>{m['app.update.reload']()}</Button>
    </SettingsRow>
  </SettingsSection>
{/if}

{#if user}
  <div class="identity">
    <Avatar name={user.displayName} />

    <div class="names">
      <p class="name">{user.displayName}</p>
      <p class="email">{user.email}</p>
    </div>

    {#if user.isAdmin}
      <Badge>{m['me.admin']()}</Badge>
    {/if}
  </div>

  <SettingsSection>
    <SettingsRow label={m['me.memberSince']()}>
      <span class="value">{formatDate(new Date(user.createdAt), { dateStyle: 'long' })}</span>
    </SettingsRow>
  </SettingsSection>
{/if}

<!-- No heading: the button says what it does. -->
<SettingsSection description={m['me.signOut.body']()} bare>
  <Button onclick={signOut}>{m['auth.signOut']()}</Button>
</SettingsSection>

<style>
  .identity {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2) var(--space-4);
    min-width: 0;
  }

  .names {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 0;
  }

  .name {
    font-size: var(--text-lg);
    font-weight: var(--weight-semibold);
    line-height: var(--leading-tight);
  }

  /* Addresses are long and have no spaces to break at. */
  .email {
    overflow-wrap: anywhere;
    color: var(--text-muted);
  }

  .value {
    color: var(--text-muted);
  }
</style>
