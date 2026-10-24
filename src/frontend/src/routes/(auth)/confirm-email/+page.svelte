<script lang="ts">
  import MailedLinkSignIn from '$features/auth/MailedLinkSignIn.svelte';
  import { m } from '$shell/i18n';
</script>

<svelte:head><title>{m['auth.confirm.title']()}</title></svelte:head>

<MailedLinkSignIn kind="confirmation" title={m['auth.confirm.title']()} />
