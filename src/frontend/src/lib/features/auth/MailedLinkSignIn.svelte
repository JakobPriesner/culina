<script lang="ts">
  import { onMount } from 'svelte';

  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import type { AppError } from '$api';
  import { Skeleton } from '$ds';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';

  import AuthIntro from './AuthIntro.svelte';
  import { takeLinkFragment } from './mailLinks';
  import { session } from './session.svelte';

  /**
   * Opens a mailed link: the token after `#` signs this browser in, then the app takes over. Sent with a POST from the
   * page, never on load of the URL itself, so a mail scanner that fetches every link uses nothing up.
   */
  interface Props {
    kind: 'sign-in' | 'confirmation';
    title: string;
  }

  let { kind, title }: Props = $props();

  let failure = $state<AppError | 'missing' | null>(null);

  onMount(async () => {
    const token = takeLinkFragment().get('token');

    if (!token) {
      failure = 'missing';

      return;
    }

    const error = await session.signInWithLink(kind, token);

    if (error) {
      failure = error;

      return;
    }

    await goto(resolve('/(app)'), { replaceState: true });
  });
</script>

<div class="link">
  {#if failure}
    <AuthIntro
      title={m['auth.link.failed.title']()}
      subtitle={failure === 'missing' ? m['problem.auth.invalid_link']() : explain(failure)}
    />
    <p class="alternative">
      <a href={resolve('/(auth)/login')}>{m['auth.reset.back']()}</a>
    </p>
  {:else}
    <div aria-busy="true">
      <AuthIntro {title} subtitle={m['auth.link.opening']()} />
      <Skeleton width="100%" height="2.75rem" shape="block" />
    </div>
  {/if}
</div>

<style>
  .link {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .alternative {
    color: var(--text-muted);
    font-size: var(--text-sm);
    text-align: center;
  }
</style>
