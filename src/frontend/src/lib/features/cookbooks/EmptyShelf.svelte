<script lang="ts">
  import { Button, EmptyState } from '$ds';
  import { m } from '$shell/i18n';
  import Olli from '$shell/olli/Olli.svelte';

  interface Props {
    /** Whether a filter is on, which makes the shelf empty by mistake rather than by being new. */
    filtered: boolean;
    /** A shelf that fills itself has nothing to put on it by hand. */
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
  <!-- Empty because nothing matches is a rule to loosen; empty because it
       is new is an invitation to add something. Different mistakes, so
       different offers. -->
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
