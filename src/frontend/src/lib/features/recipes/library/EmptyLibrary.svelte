<script lang="ts">
  import { resolve } from '$app/paths';
  import { Button, EmptyState } from '$ds';
  import { m } from '$shell/i18n';
  import Olli from '$shell/olli/Olli.svelte';
</script>

{#snippet peeking()}<Olli pose="peeking" />{/snippet}

<EmptyState title={m['recipes.empty.title']()} body={m['recipes.empty.body']()} art={peeking}>
  {#snippet icon()}
    <svg
      viewBox="0 0 48 48"
      fill="none"
      stroke="currentColor"
      stroke-width="1.5"
      aria-hidden="true"
    >
      <path
        d="M24 12c-5-4-12-5-18-3v28c6-2 13-1 18 3 5-4 12-5 18-3V9c-6-2-13-1-18 3Zm0 0v28M12 17l6 1m-6 6 6 1m12-7 6-1m-6 8 6-1"
        stroke-linecap="round"
        stroke-linejoin="round"
      />
    </svg>
  {/snippet}
  {#snippet action()}
    <Button variant="primary" href={resolve('/(app)/recipes/new')}>
      {m['recipes.empty.action']()}
    </Button>
  {/snippet}
</EmptyState>
