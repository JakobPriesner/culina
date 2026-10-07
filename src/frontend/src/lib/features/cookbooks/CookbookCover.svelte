<script lang="ts">
  import { Image } from '$ds';

  import { imageSrcset, imageUrl } from '$features/recipes/recipeImage';

  import type { CoverPicture } from './types';

  /**
   * The face of a cookbook, made of the shelf's own recipes: one fills the frame, two split it,
   * four make quarters; oldest first so it settles.
   */
  interface Props {
    /**
     * Up to four photographed recipes, oldest first; the image id goes into the address so a
     * replaced picture is a new cache key.
     */
    pictures: readonly CoverPicture[];
    name: string;
  }

  let { pictures, name }: Props = $props();

  const shown = $derived(pictures.slice(0, 4));
  const tiles = $derived(shown.length >= 4 ? 4 : shown.length >= 2 ? 2 : 1);
</script>

<!-- Decoration: hidden from screen readers since the name sits right beside it, and each tile is
     alt="". -->
<div class="cover" data-tiles={tiles} aria-hidden="true">
  {#if shown.length === 0}
    <p class="empty">{name.trim().slice(0, 1) || '·'}</p>
  {:else}
    {#each shown as { recipeId, imageId } (recipeId)}
      <div class="tile">
        <Image
          src={imageUrl(recipeId, 400, imageId)}
          srcset={imageSrcset(recipeId, imageId)}
          sizes="(min-width: 64rem) 10rem, 22vw"
          alt=""
          fill
          rounded={false}
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
