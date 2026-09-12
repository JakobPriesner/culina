<script lang="ts">
  import type { Snippet } from 'svelte';

  import { onMount } from 'svelte';

  import { cooking } from '$features/cooking/stores/cooking.svelte';
  import AppShell from '$shell/AppShell.svelte';

  interface Props {
    children: Snippet;
  }

  let { children }: Props = $props();

  // Asked once, on boot: the answer drives the bar that leads back to whatever
  // is on the hob, and it is needed on every page rather than one.
  onMount(() => void cooking.resume());
</script>

<AppShell>{@render children()}</AppShell>
