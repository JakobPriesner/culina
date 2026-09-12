<script lang="ts">
  import { onDestroy } from 'svelte';

  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { Field, TextArea, TextInput } from '$ds';
  import { createAutosave } from '$features/recipes/editor/autosave.svelte';
  import IngredientEditor from '$features/recipes/editor/IngredientEditor.svelte';
  import PhotoField from '$features/recipes/editor/PhotoField.svelte';
  import StepEditor from '$features/recipes/editor/StepEditor.svelte';
  import { changedElsewhere, recipes } from '$features/recipes/stores/recipes.svelte';
  import { m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';
  import type { Ingredient, Recipe, Step } from '$features/recipes/types';

  /**
   * Writing a recipe down.
   *
   * No Save button and no wizard: a recipe is added to for years, so there is
   * no moment at which someone is "done" and should have to say so. The work is
   * kept as it is typed and a small word says it was.
   */
  const recipeId = $derived(page.params.recipeId ?? '');

  /** The edited copy. Null until the recipe has arrived. */
  let draft = $state<Recipe | null>(null);

  const autosave = createAutosave(async () => (draft ? recipes.update(draft) : null));

  onDestroy(() => {
    void autosave.flush();
    autosave.dispose();
  });

  $effect(() => {
    if (recipeId) {
      void recipes.load(recipeId);
    }
  });

  // Taken once per recipe: after that the draft is what the author is editing,
  // and overwriting it from the store would delete what they just typed.
  $effect(() => {
    const loaded = recipes.detail;

    if (loaded && loaded.id === recipeId && draft?.id !== loaded.id) {
      draft = loaded;
    }
  });

  function change(patch: Partial<Recipe>) {
    if (!draft) {
      return;
    }

    draft = { ...draft, ...patch };
    autosave.touch();
  }

  const status = $derived.by(() => {
    if (autosave.failure && changedElsewhere(autosave.failure)) {
      return m['editor.changedElsewhere']();
    }

    switch (autosave.state) {
      case 'saving':
        return m['editor.saving']();
      case 'saved':
        return m['editor.saved']();
      case 'failed':
        return m['editor.saveFailed']();
      default:
        return '';
    }
  });
</script>

<svelte:head><title>{draft?.title ?? m['editor.new']()}</title></svelte:head>

<Page>
  {#if draft}
    <!-- Bound once so the snippets below, which are separate closures, can see
         that it is there. -->
    {@const current = draft}

    <header class="head">
      <a class="back" href={resolve('/(app)/recipes/[recipeId]', { recipeId })}>
        ← {m['editor.done']()}
      </a>

      <!-- Polite, because it reports something that already happened and must
           not interrupt whatever is being typed. -->
      <p class="status" role="status">{status}</p>
    </header>

    <div class="form">
      <Field label={m['editor.title']()}>
        {#snippet children({ id, describedBy, invalid })}
          <TextInput
            {id}
            {describedBy}
            {invalid}
            value={current.title}
            oninput={(value) => change({ title: value })}
          />
        {/snippet}
      </Field>

      <Field label={m['editor.description']()} optionalText={m['editor.optional']()}>
        {#snippet children({ id, describedBy, invalid })}
          <TextArea
            {id}
            {describedBy}
            {invalid}
            value={current.description ?? ''}
            oninput={(value) => change({ description: value || null })}
          />
        {/snippet}
      </Field>

      <PhotoField
        {recipeId}
        imageId={current.imageId}
        onchange={(imageId) => {
          // The image is saved by its own endpoint, so this only keeps the
          // draft in step — touching autosave would write the recipe again for
          // a change it does not own.
          draft = draft ? { ...draft, imageId } : draft;
        }}
      />

      <div class="numbers">
        <Field label={m['editor.yieldAmount']()}>
          {#snippet children({ id, describedBy, invalid })}
            <TextInput
              {id}
              {describedBy}
              {invalid}
              inputmode="numeric"
              value={String(current.yieldAmount)}
              oninput={(value) => change({ yieldAmount: Number(value) || 1 })}
            />
          {/snippet}
        </Field>

        <Field label={m['editor.prepMinutes']()} optionalText={m['editor.optional']()}>
          {#snippet children({ id, describedBy, invalid })}
            <TextInput
              {id}
              {describedBy}
              {invalid}
              inputmode="numeric"
              value={current.prepMinutes === null ? '' : String(current.prepMinutes)}
              oninput={(value) => change({ prepMinutes: value ? Number(value) : null })}
            />
          {/snippet}
        </Field>

        <Field label={m['editor.cookMinutes']()} optionalText={m['editor.optional']()}>
          {#snippet children({ id, describedBy, invalid })}
            <TextInput
              {id}
              {describedBy}
              {invalid}
              inputmode="numeric"
              value={current.cookMinutes === null ? '' : String(current.cookMinutes)}
              oninput={(value) => change({ cookMinutes: value ? Number(value) : null })}
            />
          {/snippet}
        </Field>
      </div>

      <section>
        <h2 class="section">{m['editor.ingredients']()}</h2>

        <IngredientEditor
          ingredients={current.groups[0]?.ingredients ?? []}
          onchange={(ingredients: Ingredient[]) =>
            change({ groups: [{ id: current.groups[0]?.id ?? null, name: null, ingredients }] })}
        />
      </section>

      <section>
        <h2 class="section">{m['editor.steps']()}</h2>

        <StepEditor
          steps={current.steps}
          ingredients={current.groups.flatMap((group) => group.ingredients)}
          onchange={(steps: Step[]) => change({ steps })}
        />
      </section>
    </div>
  {/if}
</Page>

<style>
  .head {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: var(--space-4);
    margin-bottom: var(--space-6);
  }

  .back {
    color: var(--text-muted);
    font-size: var(--text-sm);
    text-decoration: none;
  }

  .status {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .form {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
    max-width: 40rem;
  }

  .numbers {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr));
    gap: var(--space-4);
  }

  .section {
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.08em;
    text-transform: uppercase;
    color: var(--text-muted);
    margin-bottom: var(--space-3);
  }
</style>
