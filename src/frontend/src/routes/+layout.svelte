<script lang="ts">
  import '../app.css';

  import { onMount, type Snippet } from 'svelte';

  import { preferences } from '$shell/preferences.svelte';

  interface Props {
    children: Snippet;
  }

  let { children }: Props = $props();

  // Picks up what the inline script in app.html already applied, then follows
  // the device while the app is open.
  onMount(() => preferences.start());
</script>

<!--
  Re-creating the tree is what makes a language switch take effect without a
  reload: compiled messages are plain function calls, so nothing else would tell
  Svelte that every string on the page just changed. Language changes are rare
  enough that the cost never shows.
-->
{#key preferences.locale}
  {@render children()}
{/key}
