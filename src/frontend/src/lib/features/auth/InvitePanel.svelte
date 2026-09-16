<script lang="ts">
  import { Button } from '$ds';
  import { invitations } from './invitations.svelte';
  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
  import { toaster } from '$shell/toaster.svelte';

  /**
   * How a household stops being one person's.
   *
   * The code is a bearer token: whoever holds the link can join and then see
   * every recipe here. So it is said plainly, the code is shown once where it
   * can be copied, and every invitation that has not been used yet can be taken
   * back — which is the only remedy for a link sent to the wrong chat.
   *
   * The heading and the sentence warning what the link is belong to the page
   * that places this, so that every section of settings is titled the same way
   * by one component rather than three ways by three.
   */
  interface Props {
    householdId: string;
  }

  let { householdId }: Props = $props();

  let working = $state(false);

  $effect(() => {
    void invitations.load(householdId);
  });

  const linkFor = (code: string) => `${location.origin}/join/${code}`;

  const when = (iso: string) =>
    new Intl.DateTimeFormat(preferences.locale, { dateStyle: 'long' }).format(new Date(iso));

  async function create() {
    working = true;

    const failure = await invitations.create(householdId);

    working = false;

    if (failure) {
      toaster.show({ message: failure.detail, tone: 'danger' });
    }
  }

  async function copy(code: string) {
    try {
      await navigator.clipboard.writeText(linkFor(code));
      toaster.show({ message: m['me.invite.copied'](), tone: 'success' });
    } catch {
      // A browser that refuses the clipboard is not a failure worth a message:
      // the link is on screen and can be selected.
    }
  }

  async function revoke(invitationId: string) {
    const failure = await invitations.revoke(householdId, invitationId);

    toaster.show({
      message: failure ? failure.detail : m['me.invite.revoked'](),
      tone: failure ? 'danger' : 'neutral'
    });
  }
</script>

<div class="panel">
  <Button variant="primary" onclick={create} loading={working}>{m['me.invite.create']()}</Button>

  {#if invitations.freshCode}
    <div class="fresh">
      <p class="label">{m['me.invite.link']()}</p>
      <!-- Readable and selectable, not a field: there is nothing to type here,
           and an input invites somebody to edit a token. -->
      <p class="code" data-testid="invitation-link">{linkFor(invitations.freshCode)}</p>
      <p class="once">{m['me.invite.once']()}</p>
      <Button onclick={() => copy(invitations.freshCode!)}>{m['me.invite.copy']()}</Button>
    </div>
  {/if}

  <h3>{m['me.invite.outstanding']()}</h3>

  {#if invitations.items.length === 0}
    <p class="empty">{m['me.invite.none']()}</p>
  {:else}
    <ul class="list">
      {#each invitations.items as invitation (invitation.invitationId)}
        <li class="row">
          <span class="expiry">{m['me.invite.expires']({ when: when(invitation.expiresAt) })}</span>
          <Button variant="ghost" onclick={() => revoke(invitation.invitationId)}>
            {m['me.invite.revoke']()}
          </Button>
        </li>
      {/each}
    </ul>
  {/if}
</div>

<style>
  .panel {
    min-width: 0;
    max-width: 100%;
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    align-items: flex-start;
  }

  /* The caption over the list, not a section of its own: smaller than the body
     it introduces, which is what tells the eye it is a label rather than the
     start of something new. */
  h3 {
    margin-top: var(--space-2);
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
    color: var(--text-subtle);
    text-transform: uppercase;
    letter-spacing: 0.08em;
  }

  /* Raised, so the one thing on the page that cannot be read again is also the
     one thing that is impossible to scroll past. */
  .fresh {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    align-items: flex-start;
    width: 100%;
    padding: var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
  }

  .label {
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
  }

  /* Monospaced and on its own sunken line: a link somebody has to check
     character by character before sending it to the wrong person. */
  .code {
    width: 100%;
    padding: var(--space-2) var(--space-3);
    border-radius: var(--radius-md);
    background: var(--surface-sunken);
    overflow-wrap: anywhere;
    font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
    font-size: var(--text-sm);
  }

  .once {
    font-size: var(--text-sm);
    color: var(--text-muted);
  }

  .empty {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  /* Hairlines between rows rather than around each: one list, not a stack of
     little boxes. */
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
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2) var(--space-4);
    padding: var(--space-2) var(--space-2) var(--space-2) var(--space-4);
  }

  .row + .row {
    border-top: 1px solid var(--border);
  }

  .expiry {
    font-size: var(--text-sm);
    color: var(--text-muted);
  }
</style>
