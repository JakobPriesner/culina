<script lang="ts">
  import { Switch } from '$ds';
  import { m } from '$shell/i18n';

  import { variables } from './types';

  /**
   * Whether the session cookie is only sent over HTTPS.
   *
   * The one server setting that can lock the person changing it out: on, over
   * plain HTTP, the browser refuses the cookie and nobody can sign in. The
   * server cannot see this — behind a proxy that terminates TLS every request
   * it receives is plain HTTP — but the browser can, so the warning is decided
   * here, from the address the page itself was loaded from.
   */
  interface Props {
    checked: boolean;
    pinned?: boolean;
    disabled?: boolean;
  }

  let { checked = $bindable(), pinned = false, disabled = false }: Props = $props();

  const plainHttp = typeof location !== 'undefined' && location.protocol === 'http:';
</script>

<div class="secure">
  <Switch
    {checked}
    label={m['server.secure']()}
    description={pinned
      ? m['server.pinned']({ variable: variables.secure })
      : m['server.secure.hint']()}
    disabled={disabled || pinned}
    onchange={(value) => (checked = value)}
  />

  {#if checked && plainHttp}
    <p class="warning" role="alert">{m['server.secure.http']()}</p>
  {/if}
</div>

<style>
  .secure {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }

  .warning {
    max-width: var(--measure);
    color: var(--text-danger);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }
</style>
