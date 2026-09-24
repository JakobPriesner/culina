<script lang="ts">
  import { Button } from '$ds';
  import { m } from '$shell/i18n';

  import SettingField from './SettingField.svelte';
  import { variables, type ServerDraft, type ServerFacts } from './types';

  /**
   * Which proxies may say who a visitor really is.
   *
   * The hardest setting on the screen to get right from the outside, because
   * the address that matters is the proxy's as Culina sees it — a container
   * address nobody chose. So the server says what it saw on this very request,
   * and the likely answer is one button away.
   */
  interface Props {
    draft: ServerDraft;
    facts: ServerFacts;
    disabled?: boolean;
  }

  let { draft = $bindable(), facts, disabled = false }: Props = $props();

  const connection = $derived(facts.connection);
  const address = $derived(connection.remoteAddress ?? '');

  const alreadyListed = $derived(
    draft.knownProxies
      .split(',')
      .map((entry) => entry.trim())
      .includes(address)
  );

  const proxiesPinned = $derived(facts.pinned.has(variables.knownProxies));

  function trust() {
    draft.knownProxies =
      draft.knownProxies.trim().length > 0 ? `${draft.knownProxies.trim()}, ${address}` : address;
  }
</script>

<div class="seen" role="status">
  {#if connection.proxyTrusted}
    <p>{m['server.proxy.trusted']()}</p>
  {:else if connection.forwarded && address}
    <p>{m['server.proxy.untrusted']({ address })}</p>
    {#if !alreadyListed && !proxiesPinned}
      <Button variant="secondary" size="sm" {disabled} onclick={trust}>
        {m['server.proxy.trust']({ address })}
      </Button>
    {/if}
  {:else}
    <p>{m['server.proxy.direct']()}</p>
  {/if}
</div>

<SettingField
  label={m['server.proxies']()}
  hint={m['server.proxies.hint']()}
  variable={variables.knownProxies}
  pinned={proxiesPinned}
  placeholder="10.0.0.2"
  {disabled}
  bind:value={draft.knownProxies}
/>

<SettingField
  label={m['server.networks']()}
  hint={m['server.networks.hint']()}
  variable={variables.knownNetworks}
  pinned={facts.pinned.has(variables.knownNetworks)}
  placeholder="172.18.0.0/16"
  {disabled}
  bind:value={draft.knownNetworks}
/>

<style>
  .seen {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2) var(--space-3);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  .seen p {
    max-width: var(--measure);
  }
</style>
