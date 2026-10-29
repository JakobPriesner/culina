<script lang="ts">
  import { onMount } from 'svelte';

  import { http, request } from '$api';
  import { Button, Field, Skeleton, Switch, TextInput } from '$ds';
  import { session } from '$features/auth/session.svelte';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';

  /**
   * Whether this server looks barcodes up at Open Food Facts (docs/barcode-scanning-design.md §5.2). On by default, so
   * a scan finds the product, and said plainly: each new barcode and each search someone asks for leaves the server.
   * Saved at once, with no restart, so it sits apart from the server settings that restart.
   */
  let loaded = $state(false);
  let enabled = $state(true);
  let contact = $state('');
  let saving = $state(false);
  let message = $state<{ text: string; failed: boolean } | null>(null);

  onMount(async () => {
    const result = await request(() => http.GET('/api/v1/settings/products'));

    if (result.ok) {
      enabled = result.value.openFoodFacts;
      // Never filled in from the account: an address is sent only when the administrator typed it here.
      contact = result.value.contact ?? '';
    } else {
      message = { text: explain(result.error), failed: true };
    }

    loaded = true;
  });

  async function save(event: SubmitEvent) {
    event.preventDefault();
    saving = true;

    const result = await request(() =>
      http.PUT('/api/v1/settings/products', {
        body: { openFoodFacts: enabled, contact: contact.trim() || null }
      })
    );

    saving = false;
    message = result.ok
      ? { text: m['server.products.saved'](), failed: false }
      : { text: explain(result.error), failed: true };

    // The diary's scan and search read the switch from the session.
    if (result.ok) await session.refresh();
  }
</script>

{#if loaded}
  <form class="fields" onsubmit={save} novalidate>
    <Switch
      bind:checked={enabled}
      label={m['server.products.switch']()}
      description={m['server.products.switchHint']()}
      onchange={() => (message = null)}
    />
    <Field label={m['server.products.contact']()} hint={m['server.products.contactHint']()}>
      {#snippet children({ id, describedBy, invalid })}
        <TextInput
          {id}
          {describedBy}
          {invalid}
          bind:value={contact}
          autocomplete="email"
          maxlength={200}
          oninput={() => (message = null)}
        />
      {/snippet}
    </Field>
    <div class="actions">
      <Button type="submit" loading={saving}>{m['server.products.save']()}</Button>
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
