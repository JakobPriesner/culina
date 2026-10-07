<script lang="ts">
  import { Switch } from '$ds';
  import { m } from '$shell/i18n';

  import { variables } from './types';

  /**
   * Whether the session cookie is HTTPS-only: the one setting that can lock out the person changing it (on, over plain HTTP, nobody signs in).
   * The server cannot see it behind a TLS-terminating proxy, so the warning comes from the page being a secure context (https, or http://localhost).
   */
  interface Props {
    checked: boolean;
    pinned?: boolean;
    disabled?: boolean;
  }

  let { checked = $bindable(), pinned = false, disabled = false }: Props = $props();

  const plainHttp = typeof window !== 'undefined' && !window.isSecureContext;
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
