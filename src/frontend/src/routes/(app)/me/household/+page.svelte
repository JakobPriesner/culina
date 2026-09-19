<script lang="ts">
  import { Avatar, Badge } from '$ds';

  import ArchivePanel from '$features/archive/ArchivePanel.svelte';
  import InvitePanel from '$features/auth/InvitePanel.svelte';
  import MembersPanel from '$features/auth/MembersPanel.svelte';
  import { members } from '$features/auth/members.svelte';
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';

  import SettingsSection from '../SettingsSection.svelte';

  /**
   * The kitchen being shared: who is in it, how somebody else gets in, and how
   * to take the whole thing elsewhere.
   *
   * Everything here belongs to the household rather than to the person reading
   * it, which is the whole reason it is a category of its own.
   *
   * The page led with the household's name set as a bare line of text, and then
   * never answered the question its own subtitle asks. Now it opens the way the
   * account page does — the thing itself, named, with what it is at a glance —
   * and the first section is the list of people the rest of this page is about.
   *
   * The sections are separated by the space around their headings rather than
   * by the rules that used to sit between them. A line drawn across a gap that
   * was already doing the work is decoration, and there were two of them.
   */
  const household = $derived(session.activeHousehold);

  const roles: Record<string, () => string> = {
    owner: m['me.role.owner'],
    member: m['me.role.member']
  };

  /** Said under the name once it is known, and nothing before then. */
  const size = $derived(members.status === 'ready' ? members.items.length : null);
</script>

<svelte:head><title>{m['me.household']()}</title></svelte:head>

{#if household}
  <div class="identity">
    <!-- The household's own initial rather than a generic house: on a screen
         where the next panel is a column of faces, the same mark drawn the same
         way says these are the same kind of thing. -->
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

  <SettingsSection title={m['archive.title']()} description={m['archive.hint']()} bare>
    <ArchivePanel householdId={household.householdId} />
  </SettingsSection>
{:else}
  <p class="none">{m['me.household.none']()}</p>
{/if}

<style>
  /*
   * The same arrangement the account page opens with, because it is the same
   * kind of statement: this is the thing the page is about, and here is what
   * you are to it.
   */
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
