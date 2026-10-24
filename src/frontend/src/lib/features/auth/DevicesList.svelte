<script lang="ts">
  import { Badge, Button, Skeleton } from '$ds';

  import { explain } from '$shell/explain';
  import { formatDate, m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  import { describeUserAgent, devices, type Device, type DeviceKind } from './devices.svelte';

  /**
   * Every place this account is signed in, so one that is lost or no longer wanted can be signed out. The one reading
   * this is marked and cannot sign itself out here (that is the Account page's button). The heading belongs to the page.
   */

  $effect(() => {
    void devices.load();
  });

  const kinds: Record<DeviceKind, () => string> = {
    browser: m['me.devices.kind.browser'],
    ios: m['me.devices.kind.ios'],
    android: m['me.devices.kind.android'],
    instance: m['me.devices.kind.instance'],
    agent: m['me.devices.kind.agent'],
    other: m['me.devices.kind.other']
  };

  const lastSeen = (device: Device) =>
    m['me.devices.lastSeen']({
      when: formatDate(new Date(device.lastSeenAt), { dateStyle: 'medium', timeStyle: 'short' })
    });

  async function revoke(device: Device) {
    const failure = await devices.revoke(device.sessionId);

    toaster.show({
      message: () => (failure ? explain(failure) : m['me.devices.revoked']()),
      tone: failure ? 'danger' : 'neutral'
    });
  }
</script>

{#if devices.status === 'failed' && devices.items.length === 0}
  <div class="failed" role="alert">
    <p>{m['me.devices.failed']()}</p>
    <Button size="sm" onclick={() => void devices.load()}>{m['me.devices.retry']()}</Button>
  </div>
{:else if devices.items.length === 0 && devices.status !== 'ready'}
  <ul class="list" aria-busy="true" aria-label={m['me.devices.title']()}>
    {#each ['a', 'b', 'c'] as row (row)}
      <li class="row">
        <span class="what">
          <Skeleton width="10rem" height="1rem" />
          <Skeleton width="14rem" height="0.75rem" />
        </span>
      </li>
    {/each}
  </ul>
{:else}
  <ul class="list" aria-label={m['me.devices.title']()}>
    {#each devices.items as device (device.sessionId)}
      {@const kind = kinds[device.kind]()}
      {@const browser = describeUserAgent(device.userAgent)}
      <li class="row">
        <span class="what">
          <span class="name">
            {kind}
            {#if device.isCurrent}<Badge tone="accent">{m['me.devices.current']()}</Badge>{/if}
          </span>

          <span class="detail">
            {[browser, device.ipAddress, lastSeen(device)].filter(Boolean).join(' · ')}
          </span>
        </span>

        {#if !device.isCurrent}
          <Button
            size="sm"
            label={m['me.devices.revokeOne']({ device: browser ? `${kind}, ${browser}` : kind })}
            onclick={() => void revoke(device)}
          >
            {m['me.devices.revoke']()}
          </Button>
        {/if}
      </li>
    {/each}
  </ul>
{/if}

<style>
  /* Hairlines between rows, like the members list: one list, not a stack of boxes. */
  .list {
    display: flex;
    flex-direction: column;
    width: 100%;
    margin: 0;
    padding: 0;
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
    list-style: none;
  }

  .row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2) var(--space-4);
    min-width: 0;
    padding: var(--space-3) var(--space-4);
  }

  .row + .row {
    border-top: 1px solid var(--border);
  }

  .what {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 0;
    flex: 1 1 14rem;
  }

  .name {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    font-weight: var(--weight-medium);
  }

  /* Addresses and dates run long and have few spaces to break at. */
  .detail {
    color: var(--text-muted);
    font-size: var(--text-sm);
    overflow-wrap: anywhere;
  }

  .failed {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-3);
    color: var(--text-muted);
  }
</style>
