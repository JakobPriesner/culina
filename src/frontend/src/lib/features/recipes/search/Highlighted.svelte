<script lang="ts">
  import { highlightMatches } from './highlight';

  /** Marks search matches by weight, not background: yellow boxes shout, colour alone says nothing to some, and weight prints. */
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
