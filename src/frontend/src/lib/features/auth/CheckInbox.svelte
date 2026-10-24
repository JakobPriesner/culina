<script lang="ts">
  import type { Snippet } from 'svelte';

  import AuthIntro from './AuthIntro.svelte';

  /**
   * What a signed-out form turns into once a link is on its way: where to look and how long it works. Announced, as
   * it replaces the form the person just sent.
   */
  interface Props {
    title: string;
    body: string;
    children?: Snippet;
  }

  let { title, body, children }: Props = $props();
</script>

<div class="inbox" role="status">
  <AuthIntro {title} subtitle={body} />
  {@render children?.()}
</div>

<style>
  .inbox {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }
</style>
