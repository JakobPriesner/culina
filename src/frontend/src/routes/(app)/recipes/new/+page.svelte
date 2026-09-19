<script lang="ts">
  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { http, request } from '$api';
  import { Button, Field, TextInput } from '$ds';
  import FormFailure from '$features/auth/FormFailure.svelte';
  import { createSubmission } from '$features/auth/submission.svelte';
  import {
    forgetLastDraft,
    recallLastDraft,
    rememberLastDraft,
    type LastDraft
  } from '$features/recipes/editor/lastDraft';
  import PasteImport from '$features/recipes/editor/PasteImport.svelte';
  import IdeaDraft from '$features/assistance/IdeaDraft.svelte';
  import PhotographDraft from '$features/assistance/PhotographDraft.svelte';
  import { drafts } from '$features/assistance/stores/drafts.svelte';
  import { acceptEverything, toPatch, type Draft } from '$features/assistance/draftToRecipe';
  import type { ParsedRecipe } from '$features/recipes/editor/parseRecipeText';
  import { toRecipe } from '$features/recipes/mappers';
  import { recipes } from '$features/recipes/stores/recipes.svelte';
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
  import Page from '$shell/Page.svelte';
  import PageHeader from '$shell/PageHeader.svelte';
  import { onDestroy, onMount } from 'svelte';

  /**
   * Starting a recipe asks for one thing.
   *
   * A recipe with just a title is valid and saveable — it is the placeholder
   * for "I want to write this down later". The forty-field form is the reason
   * most recipes never get written down at all.
   *
   * So the page has one rank and then another, and never three things of equal
   * weight. The title field is the page: set in the editorial face at the size
   * it will be read at, in the one panel on the screen, with the only accented
   * button under it. Pasting a block of text and bringing a whole library over
   * are the other two ways in, and they sit below as two quiet doors — a door
   * being the honest shape for them, since both lead somewhere rather than
   * happening here.
   */
  let title = $state('');

  /** Whether the pasting box has been opened, which takes over the page. */
  let pasting = $state(false);
  let describing = $state(false);
  let photographing = $state(false);

  const submission = createSubmission();

  onDestroy(() => submission.dispose());

  /**
   * A recipe already started here, still nothing but its title.
   *
   * Set once, on mount: this page is about starting something, not about
   * watching a draft change underneath the form while it is open.
   */
  let continuing = $state<LastDraft | null>(null);

  onMount(() => {
    void checkForUnfinishedDraft();
  });

  async function checkForUnfinishedDraft() {
    const userId = session.user?.userId;
    const householdId = session.activeHouseholdId;

    if (!userId || !householdId) {
      return;
    }

    const kept = recallLastDraft(userId, householdId);

    if (!kept) {
      return;
    }

    const result = await request(() =>
      http.GET('/api/v1/recipes/{recipeId}', { params: { path: { recipeId: kept.recipeId } } })
    );

    if (!result.ok) {
      // Gone, or no longer this household's to see. Either way, not worth
      // offering back.
      if (result.error.status === 404) {
        forgetLastDraft(userId, householdId);
      }

      return;
    }

    const recipe = toRecipe(result.value);
    const stillEmpty =
      recipe.groups.every((group) => group.ingredients.length === 0) && recipe.steps.length === 0;

    if (stillEmpty) {
      continuing = { recipeId: recipe.id, title: recipe.title };
    } else {
      // It has ingredients or steps now — started elsewhere, or finished here
      // and simply revisited. Either way, creation is no longer unfinished.
      forgetLastDraft(userId, householdId);
    }
  }

  async function submit(event: SubmitEvent) {
    event.preventDefault();
    await start(title.trim(), null);
  }

  /**
   * Makes the recipe and opens it for editing.
   *
   * Two steps rather than one endpoint, because creating takes a title and
   * nothing else — which is the whole shape of starting a recipe here — and
   * pasted contents are an ordinary edit of a recipe that already exists. A
   * create-with-everything endpoint would be a second way to write a recipe,
   * and the second way is the one that drifts.
   */
  async function start(named: string, pasted: ParsedRecipe | null) {
    const householdId = session.activeHouseholdId;

    if (!householdId) {
      return;
    }

    let created: string | null = null;

    const ok = await submission.run(async () => {
      const outcome = await recipes.create(householdId, named || m['import.paste.untitled']());

      if ('code' in outcome) {
        return outcome;
      }

      created = outcome.id;

      if (!pasted) {
        return null;
      }

      return recipes.update({
        ...outcome,
        // Only a site that published structured data knows these. A pasted
        // block of words does not say, and the recipe keeps its defaults.
        ...(pasted.servings === undefined ? {} : { yieldAmount: pasted.servings }),
        ...(pasted.totalMinutes === undefined ? {} : { cookMinutes: pasted.totalMinutes }),
        groups: [
          {
            id: null,
            name: null,
            ingredients: pasted.ingredients.map((one) => ({
              id: '',
              quantity: one.quantity,
              name: one.name,
              note: one.note
            }))
          }
        ],
        // Plain text for now. The words are what was pasted, and an ingredient
        // is mentioned in a step by typing @ — guessing which ones were meant
        // is the silent linking this editor deliberately stopped doing.
        steps: pasted.steps.map((text) => ({
          id: null,
          title: null,
          segments: [{ kind: 'text' as const, text }],
          uses: [],
          durationSeconds: null
        }))
      });
    });

    if (ok && created) {
      const userId = session.user?.userId;

      if (userId) {
        rememberLastDraft(userId, householdId, created, named || m['import.paste.untitled']());
      }

      await goto(resolve('/(app)/recipes/[recipeId]/edit', { recipeId: created }));
    }
  }

  /**
   * The same road as a pasted recipe: create with a title, then fill it in.
   *
   * Deliberately not a review dialog first. There is nothing to compare an
   * assistant's draft against on this screen — the recipe does not exist yet —
   * and a dialog asking somebody to approve a recipe they have not read is a
   * dialog they will dismiss. The editor is the review: it opens with the draft
   * in it, unsaved changes are the norm there, and deleting a recipe they did
   * not want is one action away.
   */
  async function startFromDraft(written: Draft): Promise<void> {
    const householdId = session.activeHouseholdId;

    if (!householdId) {
      return;
    }

    let created: string | null = null;
    const named = written.title?.trim() || title.trim() || m['import.paste.untitled']();

    const ok = await submission.run(async () => {
      const outcome = await recipes.create(householdId, named);

      if ('code' in outcome) {
        return outcome;
      }

      created = outcome.id;

      return recipes.update({
        ...outcome,
        ...toPatch(written, acceptEverything(written), outcome)
      });
    });

    drafts.dismiss();

    if (ok && created) {
      const userId = session.user?.userId;

      if (userId) {
        rememberLastDraft(userId, householdId, created, named);
      }

      await goto(resolve('/(app)/recipes/[recipeId]/edit', { recipeId: created }));
    }
  }
</script>

<svelte:head><title>{m['editor.new']()}</title></svelte:head>

<Page width="reading">
  <PageHeader
    title={continuing ? m['editor.newAnother']() : m['editor.new']()}
    subtitle={m['editor.titleHint']()}
  />

  <div class="stack">
    {#if continuing}
      <!-- Above the field rather than beside it: somebody who left a recipe
           half-written is here to finish it far more often than to start a
           second one, and the row says which recipe rather than making them
           remember. -->
      <a
        class="resume"
        href={resolve('/(app)/recipes/[recipeId]/edit', { recipeId: continuing.recipeId })}
      >
        <span class="resume-text">
          <span class="resume-label">{m['editor.continueDraftLabel']()}</span>
          <span class="resume-title">{continuing.title}</span>
        </span>

        <span class="resume-arrow" aria-hidden="true">→</span>
      </a>
    {/if}

    <form class="start" onsubmit={submit} novalidate>
      <FormFailure failure={submission.failure} />

      <Field label={m['editor.title']()}>
        {#snippet children({ id, describedBy, invalid })}
          <TextInput
            {id}
            {describedBy}
            {invalid}
            size="display"
            placeholder={m['editor.titlePlaceholder']()}
            bind:value={title}
          />
        {/snippet}
      </Field>

      <Button type="submit" variant="primary" size="lg" loading={submission.showingProgress}>
        {m['editor.create']()}
      </Button>
    </form>

    {#if session.activeHouseholdId}
      {#if pasting}
        <PasteImport
          bind:open={pasting}
          householdId={session.activeHouseholdId}
          busy={submission.showingProgress}
          onimport={(parsed) => void start(title.trim() || parsed.title, parsed)}
        />
      {:else if describing}
        <IdeaDraft
          householdId={session.activeHouseholdId}
          language={preferences.locale}
          onwritten={(written) => void startFromDraft(written)}
          oncancel={() => {
            describing = false;
            drafts.dismiss();
          }}
        />
      {:else if photographing}
        <PhotographDraft
          householdId={session.activeHouseholdId}
          language={preferences.locale}
          onread={(written) => void startFromDraft(written)}
          oncancel={() => (photographing = false)}
        />
      {:else}
        <!--
          The other two ways in, and both heavier than typing a name — so they
          are offered second, as doors rather than as forms. Putting all three
          on one screen as equals would make the ten-second thing feel like the
          beginning of a migration.
        -->
        <section class="others" aria-labelledby="other-ways">
          <h2 class="others-title" id="other-ways">{m['editor.otherWays']()}</h2>

          <div class="ways">
            <!-- The whole tile is the control, so there is one label to read
                 rather than a heading, a sentence and a button repeating it. -->
            <button type="button" class="way" onclick={() => (pasting = true)}>
              <span class="way-title">{m['import.paste.title']()}</span>
              <span class="way-body">{m['import.paste.hint']()}</span>
            </button>

            <a class="way" href={resolve('/(app)/recipes/import')}>
              <span class="way-title">{m['import.source.title']()}</span>
              <span class="way-body">{m['import.source.hint']()}</span>
            </a>

            <!-- Third, and only where an assistant is connected. On every
                 other instance this door is not shut — it is not there. -->
            {#if session.user?.assistance.draft}
              <button type="button" class="way" onclick={() => (describing = true)}>
                <span class="way-title">{m['assist.idea.title']()}</span>
                <span class="way-body">{m['assist.idea.hint']()}</span>
              </button>
            {/if}

            {#if session.user?.assistance.read}
              <button type="button" class="way" onclick={() => (photographing = true)}>
                <span class="way-title">{m['assist.photo.title']()}</span>
                <span class="way-body">{m['assist.photo.hint']()}</span>
              </button>
            {/if}
          </div>
        </section>
      {/if}
    {/if}
  </div>
</Page>

<style>
  .stack {
    display: flex;
    flex-direction: column;
    gap: var(--layout-section-gap);
    min-width: 0;
  }

  /*
   * The one panel on the page, so the eye has somewhere to land.
   *
   * A card is not the default wrapper for a block of content, and this is not
   * decoration: it is the difference between "here is a form" and "start
   * here". Everything below it is deliberately outside.
   */
  .start {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-4);
    min-width: 0;
    padding: var(--space-6);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
  }

  .start :global(.field) {
    width: 100%;
  }

  .resume {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-4);
    min-width: 0;
    padding: var(--space-3) var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-sunken);
    color: inherit;
    text-decoration: none;
    transition:
      border-color var(--duration-fast) var(--ease-out),
      background-color var(--duration-fast) var(--ease-out);
  }

  .resume:hover {
    border-color: var(--border-strong);
    background: var(--surface-hover);
  }

  .resume-text {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 0;
  }

  .resume-label {
    color: var(--text-muted);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .resume-title {
    font-family: var(--font-editorial);
    font-size: var(--text-lg);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .resume-arrow {
    flex: none;
    color: var(--text-subtle);
    transition: transform var(--duration-fast) var(--ease-out);
  }

  .resume:hover .resume-arrow {
    color: var(--accent);
    transform: translateX(var(--space-1));
  }

  .others {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    min-width: 0;
  }

  /* Sentence case, not capitals. A caption set in letterspaced capitals is
     louder than the sentence inside the tile it introduces, and two lines of it
     on a phone is the loudest thing on a page whose whole argument is that
     starting a recipe is easy. */
  .others-title {
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
  }

  .ways {
    display: grid;
    gap: var(--space-3);
    min-width: 0;
  }

  /* Quiet at rest and edged on hover: two doors beside a lit one, which is the
     whole of what these are. */
  .way {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-1);
    min-width: 0;
    padding: var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: none;
    color: inherit;
    font: inherit;
    text-align: start;
    text-decoration: none;
    cursor: pointer;
    transition:
      border-color var(--duration-fast) var(--ease-out),
      background-color var(--duration-fast) var(--ease-out);
  }

  .way:hover {
    border-color: var(--border-strong);
    background: var(--surface-raised);
  }

  .way-title {
    font-weight: var(--weight-medium);
  }

  .way-body {
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  /* The page's one action, across the thumb's reach. Wide enough and it sizes
     to its own words again, where a button the width of a column would be a
     banner. */
  @media (max-width: 30rem) {
    .start :global(.button) {
      width: 100%;
    }
  }

  @media (min-width: 40rem) {
    .ways {
      grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
    }
  }
</style>
