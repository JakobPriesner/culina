<script lang="ts">
  import { highlightMatches } from './highlight';

  /**
   * Text with what a search matched in it marked by weight.
   *
   * Weight, not a background: a result list with yellow boxes in every line
   * shouts, and a colour alone says nothing to somebody who cannot see it. A
   * heavier word reads as emphasis in any theme and prints.
   */
  interface Props {
    text: string;
    /** The words searched for; nothing is marked without them. */
    query?: string;
  }

  let { text, query = '' }: Props = $props();

  const stretches = $derived(highlightMatches(text, query));
</script>

{#each stretches as stretch, index (index)}{#if stretch.matched}<mark>{stretch.text}</mark
    >{:else}{stretch.text}{/if}{/each}

<style>
  mark {
    background: none;
    color: inherit;
    font-weight: var(--weight-semibold);
  }
</style>
