<script lang="ts">
  import { FilePicker, Image, VisuallyHidden } from '$ds';

  import { formatDate, m } from '$shell/i18n';
  import { attemptSrcset, attemptUrl } from '$features/recipes/recipeImage';
  import { cookLog } from './stores/cookLog.svelte';

  /** Your own attempts, dated, newest first; square so mixed aspect ratios do not make a ragged edge. */
  interface Props {
    recipeId: string;
  }

  let { recipeId }: Props = $props();

  let busy = $state(false);
  let failure = $state<string | null>(null);
  let target = $state<string | null>(null);
  let picker = $state<ReturnType<typeof FilePicker>>();

  /** Checked here so an obviously hopeless upload fails instantly. */
  const maxBytes = 10 * 1024 * 1024;

  const shownDate = (madeAt: string) =>
    formatDate(new Date(madeAt), { day: 'numeric', month: 'short' });

  function pickFor(entryId: string) {
    target = entryId;
    failure = null;
    picker?.open();
  }

  async function chosen(file: File) {
    const entryId = target;

    if (!entryId) {
      return;
    }

    if (file.size > maxBytes) {
      failure = m['editor.photoTooLarge']();

      return;
    }

    busy = true;
    const ok = await cookLog.setPhoto(recipeId, entryId, file);

    busy = false;
    failure = ok ? null : m['editor.photoFailed']();
  }

  async function remove(entryId: string) {
    busy = true;
    const ok = await cookLog.removePhoto(recipeId, entryId);

    busy = false;
    failure = ok ? null : m['editor.photoFailed']();
  }
</script>

{#if cookLog.items.length > 0}
  <section class="attempts" aria-label={m['attempts.title']()}>
    <h3 class="title">{m['attempts.title']()}</h3>

    {#if failure}<p class="failure" role="alert">{failure}</p>{/if}

    <ul class="strip">
      {#each cookLog.items as item (item.entryId)}
        <li class="attempt">
          {#if item.hasPhoto}
            <div class="frame">
              <Image
                src={attemptUrl(recipeId, item.entryId, 400)}
                srcset={attemptSrcset(recipeId, item.entryId)}
                sizes="8rem"
                alt={m['attempts.photoAlt']({ date: shownDate(item.madeAt) })}
                ratio={1}
              />
            </div>
          {:else}
            <!-- The empty frame is the affordance; a separate "add" button would control something not on screen. -->
            <button
              class="frame empty"
              type="button"
              disabled={busy}
              onclick={() => pickFor(item.entryId)}
            >
              <!-- Decorative: the button's label carries the meaning. -->
              <svg
                aria-hidden="true"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="1.5"
              >
                <rect x="3" y="5" width="18" height="14" rx="2" />
                <circle cx="12" cy="12" r="3" />
              </svg>
              <VisuallyHidden>{m['attempts.add']({ date: shownDate(item.madeAt) })}</VisuallyHidden>
            </button>
          {/if}

          <!-- Always the second row, so dates line up across entries without a picture to remove. -->
          <p class="date">{shownDate(item.madeAt)}</p>

          {#if item.hasPhoto}
            <button
              class="action"
              type="button"
              disabled={busy}
              onclick={() => void remove(item.entryId)}
            >
              {m['attempts.remove']()}
            </button>
          {/if}
        </li>
      {/each}
    </ul>

    <FilePicker
      bind:this={picker}
      label={m['attempts.choose']()}
      accept="image/*"
      onpick={(file) => void chosen(file)}
    />
  </section>
{/if}

<style>
  .attempts {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }

  .title {
    color: var(--text-subtle);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .failure {
    color: var(--text-danger);
    font-size: var(--text-sm);
  }

  /* Scrolls sideways rather than wrapping, so the notes stay on screen. */
  .strip {
    display: flex;
    gap: var(--space-3);
    margin: 0;
    /* A scroll container clips at its padding box; room keeps the first and last focus ring visible. */
    padding: var(--space-1);
    list-style: none;
    overflow-x: auto;
    scroll-snap-type: x proximity;
  }

  .attempt {
    flex: 0 0 auto;
    width: 8rem;
    scroll-snap-align: start;
  }

  .frame {
    width: 8rem;
    height: 8rem;
    overflow: hidden;
    border-radius: var(--radius-md);
    background: var(--surface-sunken);
  }

  .empty {
    display: flex;
    align-items: center;
    justify-content: center;
    border: 1px dashed var(--border-strong);
    color: var(--text-subtle);
    cursor: pointer;
  }

  .empty:hover {
    background: var(--surface-hover);
    color: var(--text-muted);
  }

  .empty svg {
    width: var(--space-8);
    height: var(--space-8);
  }

  .date {
    margin-top: var(--space-1);
    color: var(--text-muted);
    font-size: var(--text-xs);
  }

  .action {
    margin-top: var(--space-1);
    padding: 0;
    border: 0;
    background: none;
    color: var(--text-muted);
    font: inherit;
    font-size: var(--text-xs);
    text-decoration: underline;
    cursor: pointer;
  }

  .action:hover {
    color: var(--text);
  }

  @media print {
    .attempts {
      display: none;
    }
  }
</style>
