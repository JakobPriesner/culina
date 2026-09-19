<script lang="ts">
  import { Button, FilePicker } from '$ds';
  import { http, request } from '$api';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';

  import type { Draft } from './draftToRecipe';

  /**
   * A photograph of a cookbook page, typed out.
   *
   * The one capability that does not go through the shared draft store, because
   * it does not go through the shared endpoint: a photograph arrives as
   * multipart and the other three arrive as JSON, and one route cannot bind
   * both. Everything after the request is identical.
   *
   * The size is checked here as well as on the server, so an obviously hopeless
   * upload fails instantly rather than after the photo has gone up the wire.
   */
  interface Props {
    householdId: string;
    language: string;
    onread: (draft: Draft) => void;
    oncancel: () => void;
  }

  let { householdId, language, onread, oncancel }: Props = $props();

  let picker = $state<ReturnType<typeof FilePicker>>();
  let busy = $state(false);
  let failure = $state<string | null>(null);

  /** The same ceiling an uploaded recipe photo has. */
  const maxBytes = 10 * 1024 * 1024;

  async function read(file: File): Promise<void> {
    if (file.size > maxBytes) {
      failure = m['assist.photo.tooLarge']();

      return;
    }

    busy = true;
    failure = null;

    const body = new FormData();

    body.append('file', file);

    const result = await request(() =>
      http.POST('/api/v1/recipe-drafts/photographs', {
        params: { query: { householdId, language } },
        body: body as unknown as { file: string },
        // FormData sets its own multipart boundary; serialising it as JSON
        // would send the string "[object FormData]".
        bodySerializer: (value: unknown) => value as FormData
      })
    );

    busy = false;

    if (result.ok) {
      onread(result.value);
    } else {
      failure = explain(result.error);
    }
  }
</script>

<div class="photo">
  <p class="note">{m['assist.photo.note']()}</p>

  {#if failure}
    <p class="failure" role="alert">{failure}</p>
  {/if}

  <div class="actions">
    <Button loading={busy} onclick={() => picker?.open()}>
      {m['assist.photo.choose']()}
    </Button>
    <Button variant="ghost" onclick={oncancel}>{m['assist.idea.cancel']()}</Button>
  </div>

  <FilePicker
    bind:this={picker}
    label={m['assist.photo.label']()}
    accept="image/jpeg,image/png,image/webp,image/heic"
    onpick={(file) => void read(file)}
  />

  <p class="note">{m['assist.warning']()}</p>
</div>

<style>
  .photo {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  .note {
    max-width: var(--measure);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  .failure {
    max-width: var(--measure);
    color: var(--text-danger);
    font-size: var(--text-sm);
  }
</style>
