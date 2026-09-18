<script lang="ts">
  import { ImageField } from '$ds';

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
