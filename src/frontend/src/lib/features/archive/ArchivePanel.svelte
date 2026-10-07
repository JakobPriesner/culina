<script lang="ts">
  import { base } from '$app/paths';
  import { Button, FilePicker } from '$ds';

  import { http, request } from '$api';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  /**
   * Taking recipes out and bringing them back as plain, readable JSON (a self-hosted app owes users an exit).
   * The page supplies the heading and file explanation, so each settings section is titled once.
   */
  interface Props {
    householdId: string;
  }

  let { householdId }: Props = $props();

  let restoring = $state(false);
  let picker = $state<ReturnType<typeof FilePicker>>();

  /** A plain link, not a fetch: the browser's download gives a name and progress, and archives with inline photos are the app's largest payload. */
  const href = $derived(`${base}/api/v1/households/${householdId}/archive`);

  async function restore(file: File) {
    restoring = true;

    const body = new FormData();

    body.append('file', file);

    const result = await request(() =>
      http.POST('/api/v1/households/{householdId}/archive', {
        params: { path: { householdId } },
        body: body as unknown as { file: string },
        // FormData sets its own multipart boundary; JSON-serialising it would send "[object FormData]".
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
