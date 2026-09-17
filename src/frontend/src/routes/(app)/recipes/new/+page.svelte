<script lang="ts">
  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { http, request } from '$api';
  import { Button, Card, Field, TextInput } from '$ds';
  import FormFailure from '$features/auth/FormFailure.svelte';
  import { createSubmission } from '$features/auth/submission.svelte';
  import {
    forgetLastDraft,
    recallLastDraft,
    rememberLastDraft,
    type LastDraft
  } from '$features/recipes/editor/lastDraft';
  import PasteImport from '$features/recipes/editor/PasteImport.svelte';
  import type { ParsedRecipe } from '$features/recipes/editor/parseRecipeText';
  import { toRecipe } from '$features/recipes/mappers';
  import { recipes } from '$features/recipes/stores/recipes.svelte';
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';
  import { onDestroy, onMount } from 'svelte';

  /**
   * Starting a recipe asks for one thing.
   *
   * A recipe with just a title is valid and saveable — it is the placeholder
   * for "I want to write this down later". The forty-field form is the reason
   * most recipes never get written down at all.
   */
  let title = $state('');

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
</script>

<svelte:head><title>{m['editor.new']()}</title></svelte:head>

<Page width="reading">
  <div class="stack">
    {#if continuing}
      <Card href={resolve('/(app)/recipes/[recipeId]/edit', { recipeId: continuing.recipeId })}>
        <p class="continueLabel">{m['editor.continueDraftLabel']()}</p>
        <p class="continueTitle">{continuing.title}</p>
      </Card>
    {/if}

    <form class="form" onsubmit={submit} novalidate>
      <h1 class="heading">{continuing ? m['editor.newAnother']() : m['editor.new']()}</h1>

      <FormFailure failure={submission.failure} />

      <Field label={m['editor.title']()} hint={m['editor.titleHint']()}>
        {#snippet children({ id, describedBy, invalid })}
          <TextInput {id} {describedBy} {invalid} bind:value={title} />
        {/snippet}
      </Field>

      <div>
        <Button type="submit" variant="primary" size="lg" loading={submission.showingProgress}>
          {m['editor.create']()}
        </Button>
      </div>

      {#if session.activeHouseholdId}
        <PasteImport
          householdId={session.activeHouseholdId}
          busy={submission.showingProgress}
          onimport={(parsed) => void start(title.trim() || parsed.title, parsed)}
        />
      {/if}
    </form>

    <!--
      The third way in, and the heaviest — so it is offered last and as a
      doorway rather than a form. One recipe is something you do here, in a
      field, in ten seconds; a whole library is somewhere you go for ten
      minutes. Putting them on one screen as equals would make the quick thing
      feel like the start of a migration.
    -->
    <section class="fromAnApp" aria-labelledby="from-an-app">
      <h2 id="from-an-app" class="subheading">{m['import.source.title']()}</h2>
      <p class="hint">{m['import.source.hint']()}</p>

      <div>
        <Button href={resolve('/(app)/recipes/import')}>{m['import.source.go']()}</Button>
      </div>
    </section>
  </div>
</Page>

<style>
  .stack {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
  }

  .form {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .heading {
    font-family: var(--font-editorial);
    font-size: var(--text-2xl);
    font-weight: var(--weight-regular);
  }

  .continueLabel {
    font-size: var(--text-sm);
    color: var(--text-muted);
  }

  .continueTitle {
    font-family: var(--font-editorial);
    font-size: var(--text-xl);
  }

  .fromAnApp {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    padding-top: var(--space-4);
    border-top: 1px solid var(--border);
  }

  .subheading {
    font-size: var(--text-lg);
    font-weight: var(--weight-medium);
  }

  .hint {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
