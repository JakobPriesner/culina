<script lang="ts">
  import { Avatar, Badge, Skeleton } from '$ds';

  import { formatDate, m } from '$shell/i18n';

  import { members } from './members.svelte';
  import { session } from './session.svelte';

  /**
   * Who is in this kitchen, as a list rather than a table of controls; the heading belongs to the
   * page that places this.
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
  <ul class="list" aria-busy="true" aria-label={m['me.members.title']()}>
    {#each ['a', 'b'] as row (row)}
      <li class="row">
        <Skeleton shape="circle" width="var(--space-12)" height="var(--space-12)" />
        <span class="who">
          <Skeleton width="9rem" height="1rem" />
          <Skeleton width="6rem" height="0.75rem" />
        </span>
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
  /* Hairlines between rows, like the invitations panel: one list, not a stack of boxes. */
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
