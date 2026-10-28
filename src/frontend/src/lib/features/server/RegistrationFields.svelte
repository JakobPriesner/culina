<script lang="ts">
  import { onMount } from 'svelte';

  import { http, request } from '$api';
  import { Button, Field, Skeleton, Switch, TextInput } from '$ds';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';

  /**
   * Who may create an account on this server. Closed by default, and a closed server refuses everyone, invitation or
   * not, so inviting someone needs it open (with invitations required, to keep it to them). Saved at once, with no
   * restart, so it sits apart from the server settings that restart.
   */
  let loaded = $state(false);
  let open = $state(false);
  let requireInvitation = $state(true);
  let maxUsers = $state('');
  let saving = $state(false);
  let message = $state<{ text: string; failed: boolean } | null>(null);

  onMount(async () => {
    const result = await request(() => http.GET('/api/v1/settings/registration'));

    if (result.ok) {
      open = result.value.openRegistration;
      requireInvitation = result.value.requireInvitation;
      maxUsers = String(result.value.maxUsers);
    } else {
      message = { text: explain(result.error), failed: true };
    }

    loaded = true;
  });

  async function save(event: SubmitEvent) {
    event.preventDefault();
    saving = true;

    const result = await request(() =>
      http.PUT('/api/v1/settings/registration', {
        body: {
          openRegistration: open,
          requireInvitation,
          // Not a number is sent as one the server refuses, so the answer says so rather than the box being silently zeroed.
          maxUsers: /^\d+$/.test(maxUsers.trim()) ? Number(maxUsers.trim()) : 0
        }
      })
    );

    saving = false;
    message = result.ok
      ? { text: m['server.registration.saved'](), failed: false }
      : { text: explain(result.error), failed: true };
  }
</script>

{#if loaded}
  <form class="fields" onsubmit={save} novalidate>
    <Switch
      bind:checked={open}
      label={m['server.registration.open']()}
      description={m['server.registration.openHint']()}
      onchange={() => (message = null)}
    />
    <Switch
      bind:checked={requireInvitation}
      label={m['server.registration.invitation']()}
      description={m['server.registration.invitationHint']()}
      disabled={!open}
      onchange={() => (message = null)}
    />
    <Field
      label={m['server.registration.maxUsers']()}
      hint={m['server.registration.maxUsersHint']()}
    >
      {#snippet children({ id, describedBy, invalid })}
        <TextInput
          {id}
          {describedBy}
          {invalid}
          bind:value={maxUsers}
          inputmode="numeric"
          maxlength={6}
          oninput={() => (message = null)}
        />
      {/snippet}
    </Field>
    <div class="actions">
      <Button type="submit" loading={saving}>{m['server.registration.save']()}</Button>
      {#if message}
        <p class="message" class:failed={message.failed} role="status">{message.text}</p>
      {/if}
    </div>
  </form>
{:else}
  <div class="fields" aria-busy="true">
    <Skeleton width="100%" height="3rem" shape="block" />
  </div>
{/if}

<style>
  .fields {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    padding: var(--space-4);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2) var(--space-4);
  }

  .message {
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .message.failed {
    color: var(--text-danger);
  }
</style>
