<script lang="ts">
  import { Badge } from '$ds';

  import ArchivePanel from '$features/archive/ArchivePanel.svelte';
  import InvitePanel from '$features/auth/InvitePanel.svelte';
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';

  import SettingsSection from '../SettingsSection.svelte';

  /**
   * The kitchen being shared: who else is in it, and how to take it elsewhere.
   *
   * Everything here belongs to the household rather than to the person reading
   * it, which is the whole reason it is a category of its own.
   *
   * The two panels are separated by the space around their headings rather than
   * by the rules that used to sit between them. A line drawn across a gap that
   * was already doing the work is decoration, and there were two of them.
   */
  const household = $derived(session.activeHousehold);

  const roles: Record<string, () => string> = {
    owner: m['me.role.owner'],
    member: m['me.role.member']
  };
</script>

<svelte:head><title>{m['me.household']()}</title></svelte:head>

{#if household}
  <div class="identity">
    <p class="name">{household.name}</p>
    <Badge tone="accent">{(roles[household.role] ?? roles['member'])!()}</Badge>
  </div>

  <SettingsSection title={m['me.invite.title']()} description={m['me.invite.body']()} bare>
    <InvitePanel householdId={household.householdId} />
  </SettingsSection>

  <SettingsSection title={m['archive.title']()} description={m['archive.hint']()} bare>
    <ArchivePanel householdId={household.householdId} />
  </SettingsSection>
{:else}
  <p class="none">{m['me.household.none']()}</p>
{/if}

<style>
  .identity {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-3);
    min-width: 0;
  }

  .name {
    font-size: var(--text-lg);
    font-weight: var(--weight-semibold);
    line-height: var(--leading-tight);
  }

  .none {
    max-width: var(--measure);
    color: var(--text-muted);
  }
</style>
