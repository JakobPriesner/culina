<script lang="ts">
  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { Button, Field, TextInput } from '$ds';
  import FormFailure from '$features/auth/FormFailure.svelte';
  import { createSubmission } from '$features/auth/submission.svelte';
  import PasteImport from '$features/recipes/editor/PasteImport.svelte';
  import type { ParsedRecipe } from '$features/recipes/editor/parseRecipeText';
  import { recipes } from '$features/recipes/stores/recipes.svelte';
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';
  import { onDestroy } from 'svelte';

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
          durationSeconds: null
        }))
      });
    });

    if (ok && created) {
      await goto(resolve('/(app)/recipes/[recipeId]/edit', { recipeId: created }));
    }
  }
</script>

<svelte:head><title>{m['editor.new']()}</title></svelte:head>

<Page>
  <form class="form" onsubmit={submit} novalidate>
    <h1 class="heading">{m['editor.new']()}</h1>

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
</Page>

<style>
  .form {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    max-width: 32rem;
  }

  .heading {
    font-family: var(--font-editorial);
    font-size: var(--text-2xl);
    font-weight: var(--weight-regular);
  }
</style>
