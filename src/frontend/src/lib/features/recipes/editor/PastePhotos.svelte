<script lang="ts">
  import { Button } from '$ds';
  import { m } from '$shell/i18n';

  /** Screenshots and photos of a recipe, chosen and shown before they are read. */
  interface Props {
    photos: readonly File[];
    /** A preview address for each photo, in the same order. */
    urls: readonly string[];
    disabled: boolean;
    onpick: (files: File[]) => void;
    onremove: () => void;
  }

  let { photos, urls, disabled, onpick, onremove }: Props = $props();
</script>

<div class="media">
  <label for="recipe-screenshots">{m['import.media.label']()}</label>
  <input
    id="recipe-screenshots"
    type="file"
    accept="image/jpeg,image/png,image/webp"
    multiple
    {disabled}
    onchange={(event) => onpick(Array.from(event.currentTarget.files ?? []))}
  />
  <p class="hint">{m['import.media.hint']()}</p>
  {#if photos.length}
    <div class="photos">
      {#each urls as photo, index (photo)}
        <img
          src={photo}
          alt={photos[index]?.name ?? m['import.review.photo']()}
          loading="lazy"
          decoding="async"
        />
      {/each}
    </div>
    <Button variant="ghost" onclick={onremove}>{m['import.media.remove']()}</Button>
  {/if}
</div>

<style>
  .media {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }

  .media input {
    max-width: 100%;
    font: inherit;
  }

  .hint {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .photos {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(6rem, 1fr));
    gap: var(--space-2);
  }

  .photos img {
    width: 100%;
    max-height: 12rem;
    object-fit: contain;
    border-radius: var(--radius-md);
  }
</style>
