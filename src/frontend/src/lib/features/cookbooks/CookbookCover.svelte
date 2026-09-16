<script lang="ts">
  import { Image } from '$ds';

  import { imageSrcset, imageUrl } from '$features/recipes/recipeImage';

  /**
   * The face of a cookbook.
   *
   * Made of the shelf's own recipes rather than a picture somebody had to
   * choose, because a cookbook nobody has decorated should still look like
   * something. One photograph fills the frame; two split it; four make a
   * quarter each — and past four it would be a contact sheet rather than a
   * face, which is why only four are ever asked for.
   *
   * They come back oldest first, so the face settles once there are four and
   * stops changing every time something is added. A cover you cannot learn is
   * not doing the one job a cover has.
   */
  interface Props {
    /** Up to four photographed recipes, oldest first. */
    recipeIds: readonly string[];
    /** Shown when the shelf has nothing photographed on it yet. */
    name: string;
  }

  let { recipeIds, name }: Props = $props();

  const shown = $derived(recipeIds.slice(0, 4));
  const tiles = $derived(shown.length >= 4 ? 4 : shown.length >= 2 ? 2 : 1);
</script>

<!--
  Decoration, so it is hidden from a screen reader entirely: the shelf's name is
  right beside it and reading four empty images before it would be noise. Each
  tile is alt="" for the same reason.
-->
<div class="cover" data-tiles={tiles} aria-hidden="true">
  {#if shown.length === 0}
    <!-- Not a grey box. The initial is the honest answer to "nothing here has
         been photographed", and it still gives the card something to be. -->
    <p class="empty">{name.trim().slice(0, 1) || '·'}</p>
  {:else}
    {#each shown as recipeId (recipeId)}
      <div class="tile">
        <Image
          src={imageUrl(recipeId, 400)}
          srcset={imageSrcset(recipeId)}
          sizes="(min-width: 64rem) 10rem, 22vw"
          alt=""
          fill
        />
      </div>
    {/each}
  {/if}
</div>

<style>
  .cover {
    display: grid;
    aspect-ratio: 1;
    overflow: hidden;
    border-radius: var(--radius-lg);
    background: var(--surface-sunken);
    box-shadow: var(--shadow-card);
  }

  .cover[data-tiles='1'] {
    grid-template-columns: 1fr;
  }

  .cover[data-tiles='2'] {
    grid-template-columns: 1fr 1fr;
  }

  .cover[data-tiles='4'] {
    grid-template-columns: 1fr 1fr;
    grid-template-rows: 1fr 1fr;
  }

  .tile {
    position: relative;
    min-width: 0;
    min-height: 0;
  }

  .empty {
    display: grid;
    place-items: center;
    font-family: var(--font-editorial);
    font-size: var(--text-3xl);
    color: var(--text-subtle);
  }
</style>
