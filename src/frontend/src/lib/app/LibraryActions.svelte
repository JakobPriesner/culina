<script lang="ts">
  import { Button } from '$ds';
  import { m } from './i18n';

  let { oncreatecookbook }: { oncreatecookbook: () => void } = $props();
</script>

<!-- The page's one accent, as the library's new-recipe plus is on its page. -->
<Button variant="primary" size="sm" onclick={oncreatecookbook}>
  {#snippet icon()}
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="1.8"
      aria-hidden="true"
    >
      <path d="M12 5v14M5 12h14" stroke-linecap="round" />
    </svg>
  {/snippet}
  {m['cookbooks.new.action']()}
</Button>
