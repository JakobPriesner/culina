<script lang="ts">
  import { Toaster } from '$ds';
  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';

  /**
   * The toaster as the shell draws it: labelled from the catalogue, and
   * re-created whenever the language changes — the same `{#key}` the root
   * layout wraps the app in.
   */
</script>

{#key preferences.locale}
  <Toaster label={m['app.notifications']()} dismissLabel={m['app.dismiss']()} />
{/key}
