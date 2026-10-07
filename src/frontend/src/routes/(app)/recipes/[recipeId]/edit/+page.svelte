<script lang="ts">
  import { onDestroy } from 'svelte';

  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { Button, ErrorState } from '$ds';
  import ConflictBanner from '$features/recipes/editor/ConflictBanner.svelte';
  import { createRecipeDraft } from '$features/recipes/editor/createRecipeDraft.svelte';
  import EditorLayout from '$features/recipes/editor/EditorLayout.svelte';
  import EditorRail from '$features/recipes/editor/EditorRail.svelte';
  import EditorSection from '$features/recipes/editor/EditorSection.svelte';
  import EditorSkeleton from '$features/recipes/editor/EditorSkeleton.svelte';
  import InheritedNotice from '$features/recipes/editor/InheritedNotice.svelte';
  import IngredientEditor from '$features/recipes/editor/IngredientEditor.svelte';
  import PhotoField from '$features/recipes/editor/PhotoField.svelte';
  import RecipeBasicsFields from '$features/recipes/editor/RecipeBasicsFields.svelte';
  import StepEditor from '$features/recipes/editor/StepEditor.svelte';
  import TagEditor from '$features/recipes/editor/TagEditor.svelte';
  import { tags } from '$features/cookbooks/stores/tags.svelte';
  import { tagSuggestions } from '$features/recipes/stores/tagSuggestions.svelte';
  import { withIngredients } from '$features/recipes/editor/ingredientGroups';
  import { withoutIngredients } from '$features/recipes/editor/stepUsage';
  import { recipes } from '$features/recipes/stores/recipes.svelte';
  import { units } from '$features/recipes/stores/units.svelte';
  import { session } from '$features/auth/session.svelte';
  import AssistFailure from '$features/assistance/AssistFailure.svelte';
  import DraftReview from '$features/assistance/DraftReview.svelte';
  import { saysAnything } from '$features/assistance/draftToRecipe';
  import { drafts } from '$features/assistance/stores/drafts.svelte';
  import { busy } from '$shell/busy.svelte';
  import { m } from '$shell/i18n';
  import NotFound from '$shell/NotFound.svelte';
  import Page from '$shell/Page.svelte';
  import type { Ingredient, Recipe, Step } from '$features/recipes/types';

  /**
   * Writing a recipe down.
   *
   * No Save button and no wizard: a recipe is added to for years, so there is
   * no moment at which someone is "done" and should have to say so. The work is
   * kept as it is typed and a small word says it was.
   *
   * The screen is the reading surface's other half, and is built to say so. The
   * same editorial headings name the same four parts of a recipe; the rail
   * beside them is the settings screens' rail; the amounts line up in one grid
   * the way they line up when the recipe is read back. An editor that invented
   * its own typography would be a second application bolted to the first, and
   * the seam is exactly where somebody stops trusting that what they type is
   * what will be cooked.
   */
  const recipeId = $derived(page.params.recipeId ?? '');

  const editor = createRecipeDraft(() => recipeId);

  // Unsaved text on screen is not a moment to offer anybody a reload.
  const release = busy.hold();

  onDestroy(() => {
    editor.dispose();
    release();
  });

  $effect(() => {
    if (recipeId) {
      void recipes.load(recipeId);
    }
  });

  $effect(() => editor.takeLoaded());

  const draft = $derived(editor.recipe);

  // The units this kitchen uses, so a line that says "1 Schuss Milch" reads
  // back as a Schuss of milk rather than as an ingredient called "Schuss Milch".
  $effect(() => {
    if (draft) {
      void units.load(draft.householdId);
    }
  });

  // The kitchen's tags, for the tag field, and what the recipe as last saved
  // could be tagged with — asked again after every save, since a new title is
  // a new answer once the server has it.
  $effect(() => {
    if (draft) {
      void tags.load(draft.householdId);
      void tagSuggestions.load(draft.id, draft.version);
    }
  });

  /**
   * The ingredient list, which the recipe keeps in its first group.
   *
   * Groups are the "for the dough" / "for the sauce" headings, and they stay
   * invisible until a recipe needs them, so the editor only ever writes to the
   * implicit first one. The rest are carried through a save untouched — this
   * editor cannot show them yet, and a recipe that arrived from an import with
   * real headings must not lose them to a keystroke.
   */
  const groups = $derived(draft?.groups);
  const firstGroup = $derived(groups?.[0]?.ingredients ?? []);

  // Derived from the groups alone, so the steps are handed the same array until
  // an ingredient changes rather than a new one on every keystroke.
  const allIngredients = $derived(groups?.flatMap((group) => group.ingredients) ?? []);

  function setIngredients(ingredients: Ingredient[]) {
    const kept = new Set(ingredients.map((one) => one.id));
    const gone = new Set(firstGroup.filter((one) => !kept.has(one.id)).map((one) => one.id));

    // A deleted line comes off the steps that needed it too. The server refuses
    // a step needing an ingredient the recipe no longer has, and being told
    // that on the next autosave is no way to find out you deleted something.
    // `change` spreads its patch, so an absent key and one set to undefined are
    // not the same thing — the steps are only named when they have changed.
    editor.change({
      groups: withIngredients(draft?.groups ?? [], ingredients),
      ...(gone.size > 0 ? { steps: withoutIngredients(draft?.steps ?? [], gone) } : {})
    });
  }

  /**
   * Adds an ingredient named from inside a step.
   *
   * With no amount: the author was writing the method, not measuring, and a
   * made-up quantity would be worse than a blank one they can fill in.
   */
  const addIngredient = (name: string) =>
    setIngredients([
      ...firstGroup,
      { id: '', quantity: { value: null, unit: null }, name, note: null }
    ]);

  /**
   * Asks the assistant to tidy this recipe up.
   *
   * Nothing is applied here. The answer opens a review, and only what somebody
   * ticks there reaches `change()` — which matters more in this editor than it
   * would in most, because there is no Save button: anything that reached
   * `change()` would be on its way to the server 800 ms later.
   *
   * The review opens on the first thing the assistant says rather than on the
   * last: the suggestion is worth reading as it is written, and thirty seconds
   * of a spinner on a button is thirty seconds of wondering. Accepting stays
   * shut until it has finished.
   */
  async function improve(): Promise<void> {
    if (!draft || !session.activeHouseholdId) {
      return;
    }

    await drafts.ask({
      kind: 'revision',
      householdId: session.activeHouseholdId,
      recipeId,
      language: draft.language
    });
  }

  /**
   * Whether the review is open.
   *
   * While the assistant is writing, and afterwards only if it wrote something.
   * A request that failed before a word arrived used to open the review anyway
   * and say "no changes suggested" — which hid a model that was never there
   * behind a sentence about the recipe. It fails beside the button instead,
   * the same way the idea and the photograph do.
   */
  const reviewing = $derived(drafts.asking || saysAnything(drafts.draft));

  /** Folds the accepted parts in as one change, so it is one save. */
  function acceptDraft(patch: Partial<Recipe>): void {
    editor.change(patch);
    drafts.dismiss();
  }

  const back = $derived(resolve('/(app)/recipes/[recipeId]', { recipeId }));

  const sections = $derived([
    { id: 'recipe', label: m['editor.section.recipe']() },
    { id: 'photo', label: m['editor.photo']() },
    { id: 'ingredients', label: m['editor.ingredients'](), count: firstGroup.length },
    { id: 'steps', label: m['editor.steps'](), count: draft?.steps.length ?? 0 },
    { id: 'tags', label: m['editor.tags'](), count: draft?.tags.length ?? 0 }
  ]);
</script>

<svelte:head>
  <title
    >{draft?.title ??
      (editor.inheritedFrom ? recipes.detail?.title : null) ??
      m['editor.new']()}</title
  >
</svelte:head>

<Page>
  {#if editor.inheritedFrom}
    <InheritedNotice owner={session.householdName(editor.inheritedFrom)} backHref={back} />
  {:else if draft}
    <!-- Bound once so the snippets below, which are separate closures, can see
         that it is there. -->
    {@const current = draft}

    <!-- Every page says what it is. This one's title is a text field that can
         be empty and can change while it is read aloud, so the heading is a
         sentence about the page rather than the field's value. Not shown: the
         field is right there, and printing the title twice above itself is how
         a screen fills up with things nobody asked for. -->
    <h1 class="ds-clipped">{m['editor.heading']({ title: current.title })}</h1>

    <EditorLayout>
      {#snippet rail()}
        <EditorRail
          backHref={back}
          backLabel={m['editor.done']()}
          title={current.title}
          untitled={m['editor.untitled']()}
          tone={editor.saveState.tone}
          status={editor.saveState.text}
          sectionsLabel={m['editor.sections']()}
          {sections}
        />
      {/snippet}

      {#if editor.conflicted}
        <ConflictBanner onkeepmine={editor.keepMine} ontaketheirs={editor.takeTheirs} />
      {/if}

      <EditorSection id="recipe" title={m['editor.section.recipe']()}>
        {#snippet action()}
          <!-- Only when the instance has an assistant and this capability is
               on. Absent rather than disabled: an instance with no model must
               look exactly like Culina looked before there was one. -->
          {#if session.user?.assistance.improve}
            <Button variant="secondary" size="sm" loading={drafts.asking} onclick={improve}>
              {m['assist.improve']()}
            </Button>
          {/if}
        {/snippet}

        <!-- An ask that failed has to say so here. The review dialog only
             opens on an answer, so without this the button spends a few
             seconds looking busy and then goes quiet — which is what a
             budget that is spent, a provider that is down and a bug all
             looked like. -->
        {#if drafts.error && !reviewing}
          <div class="assistFailure">
            <AssistFailure error={drafts.error} />
          </div>
        {/if}

        <RecipeBasicsFields
          recipe={current}
          typed={editor.typed}
          onchange={editor.change}
          onyield={editor.writeYield}
          onminutes={editor.writeMinutes}
        />
      </EditorSection>

      <EditorSection id="photo" title={m['editor.photo']()}>
        <PhotoField {recipeId} imageId={current.imageId} onchange={editor.photoWritten} />
      </EditorSection>

      <EditorSection id="ingredients" title={m['editor.ingredients']()} count={firstGroup.length}>
        <IngredientEditor
          ingredients={firstGroup}
          steps={current.steps}
          onchange={setIngredients}
          householdId={current.householdId}
          language={current.language}
        />
      </EditorSection>

      <EditorSection id="steps" title={m['editor.steps']()} count={current.steps.length}>
        <StepEditor
          steps={current.steps}
          ingredients={allIngredients}
          onchange={(steps: Step[]) => editor.change({ steps })}
          onaddingredient={addIngredient}
        />
      </EditorSection>

      <EditorSection id="tags" title={m['editor.tags']()} count={current.tags.length}>
        <TagEditor
          tags={current.tags}
          household={tags.items}
          suggestions={tagSuggestions.of(current.id, current.version)}
          onchange={(next) => editor.change({ tags: next })}
        />
      </EditorSection>

      <!-- The end of the method is where somebody finishes writing, so it is
           where the way out belongs. The same words as the rail's quiet link
           above: one action, offered where each of the two hands is. -->
      <footer class="finish">
        <Button variant="primary" size="lg" href={back}>{m['editor.done']()}</Button>
      </footer>
    </EditorLayout>
  {:else if recipes.detailStatus === 'failed' && recipes.detailError?.status === 404}
    <NotFound kind="recipe" level={1} />
  {:else if recipes.detailStatus === 'failed'}
    <ErrorState
      title={m['editor.failed.title']()}
      body={m['editor.failed.body']()}
      requestIdLabel={m['error.reference']()}
      requestId={recipes.detailError?.requestId}
    >
      {#snippet action()}
        <Button variant="primary" onclick={() => recipes.load(recipeId)}>
          {m['error.retry']()}
        </Button>
      {/snippet}
    </ErrorState>
  {:else}
    <EditorSkeleton />
  {/if}
</Page>

{#if reviewing && draft}
  <DraftReview
    open={true}
    draft={drafts.draft}
    current={draft}
    writing={drafts.asking}
    error={drafts.error}
    onaccept={acceptDraft}
    onclose={() => drafts.dismiss()}
  />
{/if}

<style>
  /* Inside the section whose header holds the button, so the answer to
     "why did nothing happen" is next to the thing that did nothing. */
  .assistFailure {
    margin-bottom: var(--space-4);
  }

  .finish {
    display: flex;
    justify-content: flex-start;
    padding-top: var(--space-4);
    border-top: 1px solid var(--border);
  }
</style>
