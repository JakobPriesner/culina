<script lang="ts">
  import { onMount } from 'svelte';

  import { publicOrigin } from '$api';
  import { Button, Field, SectionLabel, TextInput } from '$ds';
  import { invitations } from './invitations.svelte';
  import { readPolicy } from './registration.svelte';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
  import { toaster } from '$shell/toaster.svelte';

  /**
   * Household invitations; the code is a bearer token, so it is shown once and unused invitations
   * can be revoked. The heading belongs to the page.
   */
  interface Props {
    householdId: string;
  }

  let { householdId }: Props = $props();

  let working = $state<'share' | 'mail' | null>(null);

  /** Where to mail an invitation; offered only where the instance sends mail. */
  let address = $state('');
  let sendsMail = $state(false);

  onMount(async () => {
    sendsMail = (await readPolicy()).sendsMail ?? false;
  });

  $effect(() => {
    void invitations.load(householdId);
  });

  const linkFor = (code: string) => `${publicOrigin()}/join/${code}`;

  const when = (iso: string) =>
    new Intl.DateTimeFormat(preferences.locale, { dateStyle: 'long' }).format(new Date(iso));

  async function create(how: 'share' | 'mail') {
    working = how;

    const to = how === 'mail' ? address.trim() : undefined;
    const failure = await invitations.create(householdId, to);

    working = null;

    if (failure) {
      toaster.show({ message: () => explain(failure.fields[0] ?? failure), tone: 'danger' });

      return;
    }

    if (to) {
      address = '';
      toaster.show({ message: () => m['me.invite.mailed']({ email: to }), tone: 'success' });
    }
  }

  async function copy(code: string) {
    try {
      await navigator.clipboard.writeText(linkFor(code));
      toaster.show({ message: () => m['me.invite.copied'](), tone: 'success' });
    } catch {
      // A refused clipboard isn't worth a message: the link is on screen.
    }
  }

  async function revoke(invitationId: string) {
    const failure = await invitations.revoke(householdId, invitationId);

    toaster.show({
      message: () => (failure ? explain(failure) : m['me.invite.revoked']()),
      tone: failure ? 'danger' : 'neutral'
    });
  }
</script>

<div class="panel">
  <Button variant="primary" onclick={() => create('share')} loading={working === 'share'}>
    {m['me.invite.create']()}
  </Button>

  {#if sendsMail}
    <form
      class="mail"
      onsubmit={(event) => {
        event.preventDefault();
        void create('mail');
      }}
    >
      <Field label={m['me.invite.email']()} hint={m['me.invite.email.hint']()}>
        {#snippet children({ id, describedBy, invalid })}
          <TextInput
            {id}
            {describedBy}
            {invalid}
            type="email"
            inputmode="email"
            autocomplete="off"
            value={address}
            oninput={(typed) => (address = typed)}
          />
        {/snippet}
      </Field>
      <Button type="submit" loading={working === 'mail'} disabled={address.trim().length === 0}>
        {m['me.invite.send']()}
      </Button>
    </form>
  {/if}

  {#if invitations.freshCode}
    <div class="fresh">
      <p class="label">{m['me.invite.link']()}</p>
      <!-- Readable and selectable, not an input, which would invite editing a token. -->
      <p class="code" data-testid="invitation-link">{linkFor(invitations.freshCode)}</p>
      <p class="once">{m['me.invite.once']()}</p>
      <Button onclick={() => copy(invitations.freshCode!)}>{m['me.invite.copy']()}</Button>
    </div>
  {/if}

  <SectionLabel as="h3">{m['me.invite.outstanding']()}</SectionLabel>

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

  .mail {
    display: flex;
    flex-wrap: wrap;
    align-items: flex-end;
    gap: var(--space-3);
    width: 100%;
  }

  .mail > :global(:first-child) {
    flex: 1 1 14rem;
  }

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
