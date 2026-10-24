<script lang="ts">
  import MailedLinkSignIn from '$features/auth/MailedLinkSignIn.svelte';
  import { m } from '$shell/i18n';
</script>

<svelte:head><title>{m['auth.link.title']()}</title></svelte:head>

<MailedLinkSignIn kind="sign-in" title={m['auth.link.title']()} />
