<script lang="ts">
  import { Avatar, Badge, Skeleton } from '$ds';

  import { formatDate, m } from '$shell/i18n';

  import { members } from './members.svelte';
  import { session } from './session.svelte';

  /**
   * Who is in this kitchen.
   *
   * The household page says in its own subtitle that it is about who you cook
   * with, and then said nothing about them: the only names on it were the ones
   * on unredeemed invitations. A shared library is the whole reason a household
   * exists, and "who can see my recipes" is a question somebody should never
   * have to answer by sending an invitation to find out.
   *
   * A list, not a table of controls. Removing somebody and changing a role are
   * decisions with consequences for a library everybody has been adding to, and
   * neither belongs behind a control sitting at the end of a row.
   *
   * The heading and the sentence under it belong to the page that places this,
   * the same way the invitation panel's do — one component titles every section
   * of settings, rather than three components titling three sections three
   * ways.
   */
  interface Props {
    householdId: string;
  }

  let { householdId }: Props = $props();

  $effect(() => {
    void members.load(householdId);
  });

  const roles: Record<string, () => string> = {
    owner: m['me.role.owner'],
    member: m['me.role.member']
  };

  const you = $derived(session.user?.userId);
</script>

{#if members.status === 'failed'}
  <p class="failed">{m['me.members.failed']()}</p>
{:else if members.status === 'loading' && members.items.length === 0}
  <!-- The shape of the list that is coming, so the panels below it do not jump
       up the page and back down again. -->
  <ul class="list" aria-busy="true" aria-label={m['me.members.title']()}>
    {#each ['a', 'b'] as row (row)}
      <li class="row">
        <Skeleton width="var(--space-12)" height="var(--space-12)" />
        <span class="who"><Skeleton width="8rem" /></span>
      </li>
    {/each}
  </ul>
{:else}
  <ul class="list">
    {#each members.items as member (member.userId)}
      <li class="row">
        <Avatar name={member.displayName} />

        <span class="who">
          <span class="name">
            {member.displayName}
            <!-- Said of one row only, and quietly: a list of four names where
                 one of them is yours is a list you read differently. -->
            {#if member.userId === you}<span class="you">· {m['me.members.you']()}</span>{/if}
          </span>

          <span class="joined">
            {m['me.members.joined']({
              when: formatDate(new Date(member.joinedAt), { dateStyle: 'long' })
            })}
          </span>
        </span>

        {#if member.role === 'owner'}
          <Badge tone="accent">{(roles[member.role] ?? roles['member'])!()}</Badge>
        {/if}
      </li>
    {/each}
  </ul>
{/if}

<style>
  /* Hairlines between rows rather than around each, the same enclosure the
     invitations below it use: one list, not a stack of little boxes. */
  .list {
    display: flex;
    flex-direction: column;
    width: 100%;
    margin: 0;
    padding: 0;
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    list-style: none;
  }

  .row {
    display: flex;
    align-items: center;
    gap: var(--space-4);
    min-width: 0;
    padding: var(--space-3) var(--space-4);
  }

  .row + .row {
    border-top: 1px solid var(--border);
  }

  .who {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 0;
    /* Takes the room the avatar and the badge leave, so the badge sits at the
       end of the row rather than beside the longest name. */
    flex: 1 1 auto;
  }

  .name {
    font-weight: var(--weight-medium);
    overflow-wrap: anywhere;
  }

  .you {
    color: var(--text-subtle);
    font-weight: var(--weight-regular);
  }

  .joined {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .failed {
    color: var(--text-muted);
  }
</style>
