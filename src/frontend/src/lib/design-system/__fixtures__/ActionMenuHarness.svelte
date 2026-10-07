<script lang="ts">
  import ActionMenu from '../overlay/ActionMenu.svelte';

  /** A trigger and a menu with a heading, a button and a link. */
  interface Props {
    onchoose: () => void;
  }

  let { onchoose }: Props = $props();
</script>

<ActionMenu>
  {#snippet trigger({ popovertarget })}
    <button type="button" {popovertarget}>Open</button>
  {/snippet}

  <p class="heading">Pick one</p>
  <button class="item" type="button" onclick={onchoose}>Rename</button>
  <a class="item" href="/elsewhere">Elsewhere</a>
</ActionMenu>
