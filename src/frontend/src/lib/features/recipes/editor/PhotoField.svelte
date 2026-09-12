<script lang="ts">
  import { Button, Image } from '$ds';

  import { http, request } from '$api';
  import { m } from '$shell/i18n';
  import { imageSrcset, imageUrl } from '../recipeImage';

  /**
   * The one photo a recipe has.
   *
   * One, not a gallery: what a photo is for here is recognising the dish in a
   * list, and a second picture of the same thing does not help you do that. The
   * server re-encodes whatever is uploaded, so a 12-megapixel phone photo is
   * not what anyone downloads.
   */
  interface Props {
    recipeId: string;
    imageId: string | null;
    onchange: (imageId: string | null) => void;
  }

  let { recipeId, imageId, onchange }: Props = $props();

  let busy = $state(false);
  let failure = $state<string | null>(null);
  let input = $state<HTMLInputElement>();

  /** Checked here so an obviously hopeless upload fails instantly. */
  const maxBytes = 10 * 1024 * 1024;

  async function choose(event: Event) {
    const file = (event.target as HTMLInputElement).files?.[0];

    if (!file) {
      return;
    }

    if (file.size > maxBytes) {
      failure = m['editor.photoTooLarge']();

      return;
    }

    busy = true;
    failure = null;

    const body = new FormData();

    body.append('file', file);

    const result = await request(() =>
      http.PUT('/api/v1/recipes/{recipeId}/image', {
        params: { path: { recipeId } },
        body: body as unknown as { file: string },
        // FormData sets its own multipart boundary; serialising it as JSON
        // would send the string "[object FormData]".
        bodySerializer: (value: unknown) => value as FormData
      })
    );

    busy = false;

    if (result.ok) {
      onchange(result.value.imageId ?? null);
    } else {
      failure = m['editor.photoFailed']();
    }
  }

  async function remove() {
    busy = true;
    failure = null;

    const result = await request(() =>
      http.DELETE('/api/v1/recipes/{recipeId}/image', { params: { path: { recipeId } } })
    );

    busy = false;

    if (result.ok) {
      onchange(null);
    } else {
      failure = m['editor.photoFailed']();
    }
  }
</script>

<div class="field">
  <p class="label">{m['editor.photo']()}</p>

  {#if imageId}
    <div class="preview">
      <Image
        src={imageUrl(recipeId, 800)}
        srcset={imageSrcset(recipeId)}
        sizes="(min-width: 40rem) 30rem, 90vw"
        alt=""
        ratio={4 / 3}
      />
    </div>
  {:else}
    <p class="hint">{m['editor.photoHint']()}</p>
  {/if}

  <div class="actions">
    <Button loading={busy} onclick={() => input?.click()}>
      {imageId ? m['editor.photoReplace']() : m['editor.photoChoose']()}
    </Button>

    {#if imageId}
      <Button variant="ghost" onclick={remove}>{m['editor.photoRemove']()}</Button>
    {/if}
  </div>

  {#if failure}
    <p class="failure" role="alert">{failure}</p>
  {/if}

  <!-- Hidden because the browser's own file input cannot be styled and looks
       like nothing else in the app; the button above is the control. -->
  <input
    bind:this={input}
    class="hidden"
    type="file"
    accept="image/jpeg,image/png,image/webp"
    aria-label={m['editor.photoChoose']()}
    onchange={choose}
  />
</div>

<style>
  .field {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-3);
  }

  .label {
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
  }

  .preview {
    width: min(30rem, 100%);
  }

  .hint,
  .failure {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .failure {
    color: var(--text-danger);
  }

  .actions {
    display: flex;
    gap: var(--space-3);
  }

  .hidden {
    position: absolute;
    width: 1px;
    height: 1px;
    overflow: hidden;
    clip-path: inset(50%);
  }
</style>
