<script lang="ts">
  import { base } from '$app/paths';
  import { Button } from '$ds';

  import { http, request } from '$api';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  /**
   * Taking your recipes with you, and bringing them back.
   *
   * The right answer to "what if I stop using this", and a self-hosted app owes
   * its users one. Plain, readable JSON: somebody with no Culina at all can
   * open it and find their recipes written out in words.
   */
  interface Props {
    householdId: string;
  }

  let { householdId }: Props = $props();

  let restoring = $state(false);
  let input = $state<HTMLInputElement>();

  /**
   * A plain link, not a fetch.
   *
   * The browser's own download is what puts a file where the person expects it,
   * with a name and a progress indication — neither of which a blob assembled
   * in memory would have, and an archive with its photographs inline is the
   * largest thing this app ever sends.
   */
  const href = $derived(`${base}/api/v1/households/${householdId}/archive`);

  async function restore(event: Event) {
    const element = event.target as HTMLInputElement;
    const file = element.files?.[0];

    // Cleared straight away so choosing the same file twice still fires.
    element.value = '';

    if (!file) {
      return;
    }

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
      message: result.ok
        ? m['archive.restored']({
            restored: result.value.restored,
            skipped: result.value.skipped
          })
        : m['archive.restoreFailed'](),
      tone: result.ok ? 'success' : 'danger'
    });
  }
</script>

<section class="archive">
  <h2>{m['archive.title']()}</h2>
  <p class="hint">{m['archive.hint']()}</p>

  <div class="actions">
    <Button {href} download="culina.json">{m['archive.export']()}</Button>

    <Button loading={restoring} onclick={() => input?.click()}>
      {m['archive.restore']()}
    </Button>
  </div>

  <!-- Driven by the button beside it, so it is not a tab stop with nothing to
       see. Clipped rather than hidden, which would take it out of reach. -->
  <input
    bind:this={input}
    class="offscreen"
    type="file"
    accept="application/json,.json"
    tabindex="-1"
    aria-label={m['archive.choose']()}
    onchange={(event) => void restore(event)}
  />
</section>

<style>
  .archive {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    align-items: flex-start;
  }

  h2 {
    font-size: var(--text-lg);
  }

  .hint {
    max-width: 42rem;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .actions {
    display: flex;
    gap: var(--space-3);
  }

  .offscreen {
    position: absolute;
    width: 1px;
    height: 1px;
    margin: -1px;
    overflow: hidden;
    clip-path: inset(50%);
  }
</style>
