<script lang="ts">
  import { Button, ImageField } from '$ds';

  import { http, request } from '$api';
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';
  import { imageSrcset, imageUrl } from '../recipeImage';

  /**
   * The one photo a recipe has.
   *
   * One, not a gallery: what a photo is for here is recognising the dish in a
   * list, and a second picture of the same thing does not help you do that. The
   * server re-encodes whatever is uploaded, so a 12-megapixel phone photo is
   * not what anyone downloads.
   *
   * What a picture field looks like belongs to `ImageField`; what is left here
   * is the part that knows this one is a recipe's.
   */
  interface Props {
    recipeId: string;
    imageId: string | null;
    onchange: (imageId: string | null) => void;
  }

  let { recipeId, imageId, onchange }: Props = $props();

  let busy = $state(false);
  let failure = $state<string | null>(null);

  /** Checked here so an obviously hopeless upload fails instantly. */
  const maxBytes = 10 * 1024 * 1024;

  async function upload(file: File) {
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

  /**
   * Asks the assistant for one instead of choosing a file.
   *
   * POST rather than PUT on the same sub-resource, which is the one place in
   * this API where the two methods differ by who made the thing: PUT replaces
   * the picture with bytes being sent, POST asks the server to produce one. The
   * result travels the identical path afterwards, so a drawn picture is an
   * ordinary recipe photo in every other respect.
   */
  async function draw() {
    busy = true;
    failure = null;

    const result = await request(() =>
      http.POST('/api/v1/recipes/{recipeId}/image', { params: { path: { recipeId } } })
    );

    busy = false;

    if (result.ok) {
      onchange(result.value.imageId ?? null);
    } else {
      failure = m['assist.draw.failed']();
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

<ImageField
  label={m['editor.photo']()}
  showLabel={false}
  hint={m['editor.photoHint']()}
  chooseLabel={m['editor.photoChoose']()}
  replaceLabel={m['editor.photoReplace']()}
  removeLabel={m['editor.photoRemove']()}
  src={imageId ? imageUrl(recipeId, 800) : undefined}
  srcset={imageId ? imageSrcset(recipeId) : undefined}
  sizes="(min-width: 40rem) 30rem, 90vw"
  {busy}
  {failure}
  onpick={upload}
  onremove={remove}
/>

<!-- Beside the picker rather than inside it: choosing a file is what this
     field is for, and asking for a drawing is a different kind of act. Absent
     entirely where no assistant can draw — which includes every instance
     running a model on its own hardware, since those do not make pictures. -->
{#if session.user?.assistance.draw}
  <div class="drawing">
    <Button variant="secondary" size="sm" loading={busy} onclick={draw}>
      {m['assist.draw']()}
    </Button>
    <p class="note">{m['assist.draw.note']()}</p>
  </div>
{/if}

<style>
  .drawing {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2) var(--space-3);
    margin-top: var(--space-3);
  }

  .note {
    max-width: var(--measure);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }
</style>
