<script lang="ts">
  import { instanceOrigin, native } from '$api';
  import { Avatar, Badge, Button } from '$ds';

  import { session } from '$features/auth/session.svelte';
  import { tips } from '$features/onboarding/tips.svelte';
  import { toaster } from '$shell/toaster.svelte';
  import { signOut, switchServer } from '$features/auth/signOut';
  import { formatDate, m } from '$shell/i18n';
  import { update } from '$shell/updates.svelte';

  import SettingsRow from './SettingsRow.svelte';
  import SettingsSection from './SettingsSection.svelte';

  /** Who is signed in and the way out; deliberately the emptiest category. */
  const user = $derived(session.user);
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

<!-- Help where it is looked for: the tour and the tips, either again. -->
<SettingsSection title={m['me.help']()}>
  <SettingsRow label={m['me.help.tour']()} description={m['me.help.tourBody']()}>
    <Button onclick={() => tips.startTour()}>{m['tour.offer.start']()}</Button>
  </SettingsRow>
  <SettingsRow label={m['me.help.tips']()} description={m['me.help.tipsBody']()}>
    <Button
      onclick={() => {
        tips.showAgain();
        toaster.show({ message: () => m['me.help.tipsDone'](), tone: 'success' });
      }}
    >
      {m['me.help.tipsAgain']()}
    </Button>
  </SettingsRow>
</SettingsSection>

<!-- The app talks to one self-hosted server; the website is that server. -->
{#if native}
  <SettingsSection description={m['me.server.switchBody']()}>
    <!-- One row like every other setting: the button outside it sat on the enclosure's edge with no padding. -->
    <SettingsRow label={m['me.server']()} description={instanceOrigin() ?? undefined}>
      <Button onclick={switchServer}>{m['me.server.switch']()}</Button>
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
