<script lang="ts">
  import { Image } from '$ds';

  import { imageSrcset, imageUrl } from '../recipeImage';

  /**
   * The recipe's photograph, as a banner above the title.
   *
   * Shown while reading and gone while cooking: it is what makes you choose the
   * recipe, and it is dead weight once you are standing at the hob with your
   * hands full.
   */
  interface Props {
    recipeId: string;
    imageId: string;
    /** Where the photograph is, when it is not at the recipe's own address. */
    photo?: { readonly src: string; readonly srcset: string };
  }

  let { recipeId, imageId, photo }: Props = $props();
</script>

<div class="hero">
  <Image
    src={photo?.src ?? imageUrl(recipeId, 1600, imageId)}
    srcset={photo?.srcset ?? imageSrcset(recipeId, imageId)}
    sizes="(min-width: 72rem) 72rem, 100vw"
    alt=""
    loading="eager"
    fill
    rounded={false}
  />
</div>

<style>
  /* A 16:9 box, capped so that on a wide screen the photograph does not take
     the whole first screenful. The cap shrinks the box rather than cutting the
     picture off at the bottom: what is worth looking at in a photograph of
     dinner is in the middle of it, so the crop has to come off both ends. */
  .hero {
    display: grid;
    aspect-ratio: 16 / 9;
    max-height: 24rem;
    /* Stated, not left auto: a max-height transfers through an aspect ratio
       into a max-width, and an auto width would obey it — the box would go
       narrow instead of short. */
    width: 100%;
    overflow: hidden;
    border-radius: var(--radius-lg);
  }

  /* Reading is the job on a phone, so the photograph becomes a wide banner. */
  @media (width < 52rem) {
    .hero {
      aspect-ratio: 4 / 1;
      max-height: 7rem;
      border-radius: var(--radius-md);
    }
  }

  /* It is a page of ink once you have chosen the recipe, and the paper is
     going on a worktop next to something wet, not on a wall. */
  @media print {
    .hero {
      display: none !important;
    }
  }
</style>
