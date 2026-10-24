<script lang="ts">
  import AgentConsent from '$features/auth/AgentConsent.svelte';
  import AuthIntro from '$features/auth/AuthIntro.svelte';
  import FormFailure from '$features/auth/FormFailure.svelte';
  import { m } from '$shell/i18n';

  let { data } = $props();

  // To the app, which is another site: a navigation, not a route of this one.
  const leave = (redirectUri: string) => location.assign(redirectUri);
</script>

<svelte:head><title>{m['authorize.eyebrow']()}</title></svelte:head>

{#if data.authorization}
  <AgentConsent authorization={data.authorization} onanswered={leave} />
{:else}
  <AuthIntro
    eyebrow={m['authorize.eyebrow']()}
    title={m['authorize.failed.title']()}
    subtitle={m['authorize.failed.retry']()}
    long
  />
  <FormFailure failure={data.problem} />
{/if}
