<script lang="ts">
  import { Avatar, Badge } from '$ds';

  import ArchivePanel from '$features/archive/ArchivePanel.svelte';
  import InheritancePanel from '$features/auth/InheritancePanel.svelte';
  import InvitePanel from '$features/auth/InvitePanel.svelte';
  import MembersPanel from '$features/auth/MembersPanel.svelte';
  import { members } from '$features/auth/members.svelte';
  import { session } from '$features/auth/session.svelte';
  import TrashPanel from '$features/trash/TrashPanel.svelte';
  import DeleteHouseholdPanel from '$features/auth/DeleteHouseholdPanel.svelte';
  import DeletedHouseholdList from '$features/auth/DeletedHouseholdList.svelte';
  import { deletedHouseholds, type DeletedHousehold } from '$features/auth/households.svelte';
  import { m } from '$shell/i18n';

  import SettingsSection from '../SettingsSection.svelte';

  /** The kitchen being shared: members, how somebody joins, and how to take it elsewhere. Everything here belongs to the household, not the reader. */
  const household = $derived(session.activeHousehold);

  const roles: Record<string, () => string> = {
    owner: m['me.role.owner'],
    member: m['me.role.member']
  };

  /** Households in the bin this person owns; asked once per visit, and the read touches no state so the effect cannot re-run it. */
  let deleted = $state<readonly DeletedHousehold[]>([]);

  $effect(() => {
    void deletedHouseholds().then((result) => {
      deleted = result.ok ? result.value : [];
    });
  });

  const size = $derived(members.status === 'ready' ? members.items.length : null);
</script>

<svelte:head><title>{m['me.household']()}</title></svelte:head>

{#if household}
  <div class="identity">
    <!-- The household's own initial, matching the faces in the next panel. -->
    <Avatar name={household.name} />

    <div class="names">
      <p class="name">{household.name}</p>
      {#if size !== null}
        <p class="size">{m['me.members.count']({ count: size })}</p>
      {/if}
    </div>

    <Badge tone="accent">{(roles[household.role] ?? roles['member'])!()}</Badge>
  </div>

  <SettingsSection title={m['me.members.title']()} description={m['me.members.body']()} bare>
    <MembersPanel householdId={household.householdId} />
  </SettingsSection>

  <SettingsSection title={m['me.invite.title']()} description={m['me.invite.body']()} bare>
    <InvitePanel householdId={household.householdId} />
  </SettingsSection>

  <SettingsSection
    title={m['household.inherit.title']()}
    description={m['household.inherit.body']()}
    bare
  >
    <InheritancePanel householdId={household.householdId} />
  </SettingsSection>

  <SettingsSection title={m['trash.title']()} description={m['trash.body']()} bare>
    <TrashPanel householdId={household.householdId} />
  </SettingsSection>

  <SettingsSection title={m['archive.title']()} description={m['archive.hint']()} bare>
    <ArchivePanel householdId={household.householdId} />
  </SettingsSection>

  {#if household.role === 'owner'}
    <SettingsSection
      title={m['household.delete.section']()}
      description={m['household.delete.hint']()}
      bare
    >
      <DeleteHouseholdPanel householdId={household.householdId} name={household.name} />
    </SettingsSection>
  {/if}
{:else}
  <p class="none">{m['me.household.none']()}</p>
{/if}

{#if deleted.length > 0}
  <SettingsSection
    title={m['household.deleted.title']()}
    description={m['household.deleted.body']()}
    bare
  >
    <DeletedHouseholdList items={deleted} />
  </SettingsSection>
{/if}

<style>
  /* Same arrangement the account page opens with: the thing itself, and what you are to it. */
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

  .size {
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-variant-numeric: tabular-nums;
  }

  .none {
    max-width: var(--measure);
    color: var(--text-muted);
  }
</style>
