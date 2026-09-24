<script lang="ts">
  import { base } from '$app/paths';
  import { Button, FilePicker } from '$ds';

  import { http, request } from '$api';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  /**
   * Taking your recipes with you, and bringing them back.
   *
   * The right answer to "what if I stop using this", and a self-hosted app owes
   * its users one. Plain, readable JSON: somebody with no Culina at all can
   * open it and find their recipes written out in words.
   *
   * The heading and the sentence explaining what is in the file belong to the
   * page that places this, so every section of settings is titled once, by one
   * component.
   */
  interface Props {
    householdId: string;
  }

  let { householdId }: Props = $props();

  let restoring = $state(false);
  let picker = $state<ReturnType<typeof FilePicker>>();

  /**
   * A plain link, not a fetch.
   *
   * The browser's own download is what puts a file where the person expects it,
   * with a name and a progress indication — neither of which a blob assembled
   * in memory would have, and an archive with its photographs inline is the
   * largest thing this app ever sends.
   */
  const href = $derived(`${base}/api/v1/households/${householdId}/archive`);

  async function restore(file: File) {
    restoring = true;

    const body = new FormData();

    body.append('file', file);

    const result = await request(() =>
      http.POST('/api/v1/households/{householdId}/archive', {
        params: { path: { householdId } },
        body: body as unknown as { file: string },
        // FormData sets its own multipart boundary; serialising it as JSON
        // would send the string "[object FormData]".
        bodySerializer: (value: unknown) => value as FormData
      })
    );

    restoring = false;

    toaster.show({
      message: () =>
        result.ok
          ? m['archive.restored']({
              restored: result.value.restored,
              skipped: result.value.skipped
            })
          : m['archive.restoreFailed'](),
      tone: result.ok ? 'success' : 'danger'
    });
  }
</script>

<div class="archive">
  <div class="actions">
    <Button {href} download="culina.json">{m['archive.export']()}</Button>

    <Button loading={restoring} onclick={() => picker?.open()}>
      {m['archive.restore']()}
    </Button>
  </div>

  <FilePicker
    bind:this={picker}
    label={m['archive.choose']()}
    accept="application/json,.json"
    onpick={(file) => void restore(file)}
  />
</div>

<style>
  .archive {
    min-width: 0;
    max-width: 100%;
    display: flex;
    flex-direction: column;
    align-items: flex-start;
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-3);
  }
</style>
