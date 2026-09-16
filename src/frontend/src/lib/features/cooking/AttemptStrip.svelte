<script lang="ts">
  import { FilePicker, Image, VisuallyHidden } from '$ds';

  import { m } from '$shell/i18n';
  import { attemptSrcset, attemptUrl } from '$features/recipes/recipeImage';
  import { cookLog } from './stores/cookLog.svelte';
  import { preferences } from '$shell/preferences.svelte';

  /**
   * Your own attempts, dated, newest first.
   *
   * "Made 7×" becomes seven photographs, which is a better record of a recipe
   * than any rating: it is what the thing actually looked like when *you* made
   * it, on a day you remember. The recipe's own photograph is the household's
   * and says what the dish is supposed to look like; these say what happened.
   *
   * Square, because a strip of mixed aspect ratios is a ragged edge and nobody
   * is admiring the composition of a Tuesday dinner.
   */
  interface Props {
    recipeId: string;
  }

  let { recipeId }: Props = $props();

  let busy = $state(false);
  let failure = $state<string | null>(null);
  /** Which attempt an upload is being chosen for. */
  let target = $state<string | null>(null);
  let picker = $state<ReturnType<typeof FilePicker>>();

  /** Checked here so an obviously hopeless upload fails instantly. */
  const maxBytes = 10 * 1024 * 1024;

  const dates = $derived(
    new Intl.DateTimeFormat(preferences.locale, { day: 'numeric', month: 'short' })
  );

  const shownDate = (madeAt: string) => dates.format(new Date(madeAt));

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
            <!-- An empty frame is the affordance. A separate "add" button would
                 be a control for a thing that is not on screen. -->
            <button
              class="frame empty"
              type="button"
              disabled={busy}
              onclick={() => pickFor(item.entryId)}
            >
              <!-- Decorative: the button's own label carries the meaning, and
                   without this the empty frame reads as a picture. -->
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

          <!-- Always the second row, so the dates line up across a strip whose
               entries do not all have a picture to remove. -->
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

    <!-- Driven by the frames rather than reached on its own. -->
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

  /* Scrolls sideways rather than wrapping: "made 23×" is a row you flick
     through, not a wall that pushes the notes off the screen. */
  .strip {
    display: flex;
    gap: var(--space-3);
    margin: 0;
    padding: 0 0 var(--space-1);
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

  /* A page of photographs is what a printed recipe least needs. */
  @media print {
    .attempts {
      display: none;
    }
  }
</style>
