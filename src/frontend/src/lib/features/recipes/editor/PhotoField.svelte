<script lang="ts">
  import { Button, ImageField, type ButtonVariant } from '$ds';

  import { ask, clientError, http, request, type AppError } from '$api';
  import AssistFailure from '$features/assistance/AssistFailure.svelte';
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';
  import Olli from '$shell/olli/Olli.svelte';
  import { olliSetting } from '$shell/olli/setting.svelte';
  import { imageSrcset, imageUrl } from '../recipeImage';

  /** The one photo a recipe has; the server re-encodes uploads. Looks come from `ImageField`. */
  interface Props {
    recipeId: string;
    imageId: string | null;
    onchange: (imageId: string | null) => void;
  }

  /**
   * One event of the drawing stream; not in the generated client, which only describes requests
   * that end.
   */
  interface DrawingEvent {
    seconds: number;
    finished: boolean;
    recipe?: { imageId?: string | null } | null;
    problem?: { code: string; detail: string } | null;
  }

  let { recipeId, imageId, onchange }: Props = $props();

  const canDraw = $derived(session.user?.assistance.draw ?? false);

  let busy = $state(false);
  /**
   * Kept apart from `busy`: an upload is seconds of network, a drawing is a minute of the
   * provider's work.
   */
  let drawing = $state(false);
  /**
   * Elapsed drawing time as the server counts it, so a throttled background tab shows the real
   * time.
   */
  let drawnFor = $state(0);
  let failure = $state<string | null>(null);
  /** The error itself, shown under the field because it often links to the assistant settings. */
  let drawFailure = $state<AppError | null>(null);

  const maxBytes = 10 * 1024 * 1024;

  async function upload(file: File) {
    if (file.size > maxBytes) {
      failure = m['editor.photoTooLarge']();

      return;
    }

    busy = true;
    failure = null;
    drawFailure = null;

    const body = new FormData();

    body.append('file', file);

    const result = await request(() =>
      http.PUT('/api/v1/recipes/{recipeId}/image', {
        params: { path: { recipeId } },
        body: body as unknown as { file: string },
        // FormData sets its own multipart boundary; JSON-serialising it would send "[object
        // FormData]".
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
   * Asks the assistant to draw one; a stream because it is slow (16s to 2min), and its ticks keep
   * proxies from closing the request.
   */
  function draw() {
    drawing = true;
    drawnFor = 0;
    failure = null;
    drawFailure = null;

    const stream = ask<DrawingEvent>(`/api/v1/recipes/${recipeId}/image`, null, {
      message: (event) => {
        drawnFor = event.seconds;

        if (!event.finished) {
          return;
        }

        stream.close();
        drawing = false;

        if (event.recipe) {
          onchange(event.recipe.imageId ?? null);
        } else if (event.problem) {
          drawFailure = clientError(event.problem.code, event.problem.detail);
        } else {
          failure = m['assist.draw.failed']();
        }
      },
      failed: (error) => {
        drawing = false;
        drawFailure = error;
      }
    });
  }

  async function remove() {
    busy = true;
    failure = null;
    drawFailure = null;

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
  dropLabel={m['editor.photoDrop']()}
  src={imageId ? imageUrl(recipeId, 800, imageId) : undefined}
  srcset={imageId ? imageSrcset(recipeId, imageId) : undefined}
  sizes="(min-width: 40rem) 30rem, 90vw"
  {busy}
  generating={drawing}
  generatingLabel={drawnFor > 0
    ? m['assist.draw.elapsed']({ seconds: drawnFor })
    : m['assist.draw.working']()}
  generatingArt={drawingArt}
  animateGeneration={olliSetting.animated}
  extraAction={canDraw ? drawAction : undefined}
  {failure}
  onpick={upload}
  onremove={remove}
/>

{#snippet drawingArt()}
  <Olli pose="drawing" size="md" working={drawing} />
{/snippet}

<!-- Absent where no assistant can draw (including self-hosted models). -->
{#snippet drawAction(variant: ButtonVariant)}
  <Button {variant} size="sm" disabled={busy || drawing} onclick={draw}>
    {m['assist.draw']()}
  </Button>
{/snippet}

{#if drawFailure}
  <div class="drawFailure">
    <AssistFailure error={drawFailure} />
  </div>
{/if}

{#if canDraw && !imageId}
  <p class="note">{m['assist.draw.note']()}</p>
{/if}

<style>
  .drawFailure {
    margin-top: var(--space-2);
  }

  .note {
    margin-top: var(--space-2);
    max-width: var(--measure);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }
</style>
