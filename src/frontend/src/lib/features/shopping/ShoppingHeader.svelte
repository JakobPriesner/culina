<script lang="ts">
  import { Button } from '$ds';

  import { m } from '$shell/i18n';

  interface Props {
    /** How many lines are still to find. */
    remaining: number;
    onaddrecipe: () => void;
  }

  let { remaining, onaddrecipe }: Props = $props();
</script>

<header class="head">
  <div class="titles">
    <h1 class="title">{m['shopping.title']()}</h1>
    <p class="subtitle">
      {#if remaining > 0}
        {m['shopping.remaining']({ count: remaining })}
      {:else}
        {m['shopping.subtitle']()}
      {/if}
    </p>
  </div>

  <!-- Beside the title rather than among the fields below it: both fill the
       list, but one line and twelve are different enough acts that putting
       their controls together makes the wrong one easy to hit. -->
  <Button onclick={onaddrecipe}>
    {#snippet icon()}
      <svg
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="1.8"
        stroke-linecap="round"
        stroke-linejoin="round"
        aria-hidden="true"
      >
        <!-- The same closed book the cookbooks tab is marked with, with the
             plus that means "one of these, onto the list". -->
        <path d="M6.5 3.5H17a1 1 0 0 1 1 1v7.2" />
        <path d="M18 17.5v3" />
        <path d="M4.5 6.5v11a2 2 0 0 0 2 2H14" />
        <path d="M8 3.5v16" />
        <path d="M16.5 19h3" />
      </svg>
    {/snippet}

    {m['shopping.addRecipe']()}
  </Button>
</header>

<style>
  .head {
    display: flex;
    flex-wrap: wrap;
    align-items: flex-end;
    justify-content: space-between;
    gap: var(--space-4);
    margin-bottom: var(--space-6);
  }

  .titles {
    min-width: 0;
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-display);
    font-weight: var(--weight-regular);
    letter-spacing: -0.03em;
  }

  .subtitle {
    color: var(--text-muted);
    font-size: var(--text-sm);
    margin-top: var(--space-2);
    font-variant-numeric: tabular-nums;
  }
</style>
