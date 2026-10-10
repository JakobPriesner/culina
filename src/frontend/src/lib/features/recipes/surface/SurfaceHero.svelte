<script lang="ts">
  import { Image } from '$ds';

  import { imageSrcset, imageUrl } from '../recipeImage';

  interface Props {
    recipeId: string;
    imageId: string;
    /** Overrides the recipe's own image URLs. */
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
    fetchpriority="high"
    fill
    rounded={false}
  />
</div>

<style>
  /* 16:9, capped so a wide screen does not fill the first screenful; it shrinks the box rather than cropping the bottom, since the subject is central. */
  .hero {
    display: grid;
    aspect-ratio: 16 / 9;
    max-height: 24rem;
    /* Explicit: max-height transfers through the aspect ratio into a max-width, so auto would make the box narrow instead of short. */
    width: 100%;
    overflow: hidden;
    border-radius: var(--radius-lg);
  }

  @media (width < 52rem) {
    .hero {
      aspect-ratio: 4 / 3;
      max-height: 18rem;
      border-radius: var(--radius-md);
    }
  }

  @media print {
    .hero {
      display: none !important;
    }
  }
</style>
