<script lang="ts">
  import { Button, EmptyState } from '$ds';
  import { m } from '$shell/i18n';
  import Olli from '$shell/olli/Olli.svelte';

  interface Props {
    /** A filter is on: empty by mistake rather than by being new. */
    filtered: boolean;
    /** Fills itself, so there is nothing to add by hand. */
    automatic: boolean;
    onclear: () => void;
    onadd: () => void;
    onedit: () => void;
  }

  let { filtered, automatic, onclear, onadd, onedit }: Props = $props();
</script>

{#snippet peeking()}<Olli pose="peeking" />{/snippet}

{#if filtered}
  <EmptyState
    title={m['cookbooks.detail.filtered.title']()}
    body={m['cookbooks.detail.filtered.body']()}
  >
    {#snippet action()}
      <Button onclick={onclear}>{m['recipes.filtered.action']()}</Button>
    {/snippet}
  </EmptyState>
{:else}
  <!-- No match is a rule to loosen; new is an invitation to add: different offers. -->
  <EmptyState
    title={m['cookbooks.detail.empty.title']()}
    body={automatic ? m['cookbooks.detail.noMatch.body']() : m['cookbooks.detail.empty.body']()}
    art={!automatic ? peeking : undefined}
  >
    {#snippet action()}
      {#if automatic}
        <Button variant="primary" onclick={onedit}>{m['cookbooks.rules.edit']()}</Button>
      {:else}
        <Button variant="primary" onclick={onadd}>{m['cookbooks.addRecipes.action']()}</Button>
      {/if}
    {/snippet}
  </EmptyState>
{/if}
