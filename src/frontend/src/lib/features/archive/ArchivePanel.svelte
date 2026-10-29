<script lang="ts">
  import { Button, FilePicker } from '$ds';

  import { fetchFile, native, type AppError } from '$api';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  import { restoreArchive } from './restoreArchive';

  /**
   * Taking recipes out and bringing them back as plain, readable JSON (a self-hosted app owes users an exit).
   * The page supplies the heading and file explanation, so each settings section is titled once.
   */
  interface Props {
    householdId: string;
  }

  let { householdId }: Props = $props();

  let restoring = $state(false);
  let exporting = $state(false);
  let picker = $state<ReturnType<typeof FilePicker>>();

  /** A plain link, not a fetch: the browser's download gives a name and progress, and archives with inline photos are the app's largest payload. */
  const href = $derived(`/api/v1/households/${householdId}/archive`);

  /**
   * The app cannot follow that link: it signs in with a bearer token a link does not send, and a web view has no
   * download manager. It reads the archive itself and hands it to the system share sheet, which can save it to Files.
   */
  async function exportFromApp() {
    exporting = true;

    const blob = await fetchFile(`/api/v1/households/${householdId}/archive`);

    exporting = false;

    if (!blob) {
      toaster.show({ message: () => m['error.unexpected.title'](), tone: 'danger' });

      return;
    }

    const file = new File([blob], 'culina.json', { type: 'application/json' });

    if (navigator.canShare?.({ files: [file] })) {
      await navigator.share({ files: [file] }).catch(() => {});

      return;
    }

    const url = URL.createObjectURL(file);
    const link = Object.assign(document.createElement('a'), { href: url, download: file.name });

    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 60_000);
  }

  /** Why it failed, with the id support can look up; the id is absent when the request never reached the server. */
  function failureMessage(error: AppError) {
    const reason = m['archive.restoreFailed']({ reason: explain(error) });

    return error.requestId ? `${reason} ${m['error.reference']()}: ${error.requestId}` : reason;
  }

  async function restore(file: File) {
    restoring = true;

    const result = await restoreArchive(householdId, file);

    restoring = false;

    toaster.show({
      message: () =>
        result.ok
          ? m['archive.restored']({
              restored: result.value.restored,
              skipped: result.value.skipped
            })
          : failureMessage(result.error),
      tone: result.ok ? 'success' : 'danger'
    });
  }
</script>

<div class="archive">
  <div class="actions">
    {#if native}
      <Button loading={exporting} onclick={() => void exportFromApp()}
        >{m['archive.export']()}</Button
      >
    {:else}
      <Button {href} download="culina.json">{m['archive.export']()}</Button>
    {/if}

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
