<script lang="ts">
  import { onDestroy } from 'svelte';

  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { Button, Field, TextArea, TextInput } from '$ds';
  import { createAutosave } from '$features/recipes/editor/autosave.svelte';
  import IngredientEditor from '$features/recipes/editor/IngredientEditor.svelte';
  import PhotoField from '$features/recipes/editor/PhotoField.svelte';
  import StepEditor from '$features/recipes/editor/StepEditor.svelte';
  import { withoutIngredients } from '$features/recipes/editor/stepUsage';
  import { yieldNoun } from '$features/recipes/yieldWords';
  import { ErrorCodes, type AppError } from '$api';
  import { forget, recall, remember } from '$features/recipes/editor/journal';
  import { changedElsewhere, recipes } from '$features/recipes/stores/recipes.svelte';
  import { units } from '$features/recipes/stores/units.svelte';
  import { session } from '$features/auth/session.svelte';
  import { busy } from '$shell/busy.svelte';
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

  const autosave = createAutosave(async () => {
    if (!draft) {
      return null;
    }

    const failure = await recipes.update(draft);

    if (!failure) {
      adoptSaved();
    }

    // Dropped only once the server has it. A failed save leaves the journal
    // exactly where it was, which is the whole point of writing it first.
    if (!failure && session.user) {
      forget(session.user.userId, recipeId);
      unsent = false;
    }

    return failure;
  });

  // Unsaved text on screen is not a moment to offer anybody a reload.
  const release = busy.hold();

  onDestroy(() => {
    void autosave.flush();
    autosave.dispose();
    release();
  });

  $effect(() => {
    if (recipeId) {
      void recipes.load(recipeId);
    }
  });

  // The units this kitchen uses, so a line that says "1 Schuss Milch" reads
  // back as a Schuss of milk rather than as an ingredient called "Schuss Milch".
  $effect(() => {
    if (draft) {
      void units.load(draft.householdId);
    }
  });

  /** Whether what is on screen exists only on this device. */
  let unsent = $state(false);

  /** True when this editor opened onto work a previous visit had not saved. */
  let recovered = $state(false);

  // Taken once per recipe: after that the draft is what the author is editing,
  // and overwriting it from the store would delete what they just typed.
  //
  // What this device kept wins over what the server has. It is newer by
  // definition — it exists precisely because it never reached the server — and
  // the alternative is opening an editor onto an older version of somebody's
  // own sentence.
  $effect(() => {
    const loaded = recipes.detail;

    if (!loaded || loaded.id !== recipeId || draft?.id === loaded.id) {
      return;
    }

    const kept = session.user ? recall(session.user.userId, recipeId) : null;

    draft = kept?.recipe ?? loaded;
    unsent = kept !== null;
    recovered = kept !== null;
  });

  /**
   * Takes what the server assigned, and nothing else.
   *
   * The draft is otherwise never overwritten from the store — that would delete
   * whatever was typed while the save was in the air — but two things have to
   * come back from it.
   *
   * The **version**, because every write carries the one the author last saw
   * and the server hands out a new one each time. A draft that keeps the
   * version it opened with saves exactly once and then tells the author that
   * somebody else changed their recipe, which is both wrong and alarming.
   *
   * The **ingredient ids**, because a line with no id cannot be mentioned in a
   * step. Without them the author would have to reload the page before they
   * could point a sentence at a line they wrote a moment ago. Matched by name
   * and only onto lines that have none yet, so a line added or removed
   * mid-save can at worst miss an id rather than inherit the wrong one; the
   * next save fills it in.
   */
  function adoptSaved() {
    const saved = recipes.detail;

    if (!draft || saved?.id !== draft.id) {
      return;
    }

    // Taken from the pool as they are used, so two lines of the same name get
    // an id each rather than both getting the first one.
    const unclaimed = saved.groups.flatMap((group) => group.ingredients);

    const claim = (name: string): string => {
      const at = unclaimed.findIndex((one) => one.name.toLowerCase() === name.toLowerCase());

      return at === -1 ? '' : unclaimed.splice(at, 1)[0]!.id;
    };

    draft = {
      ...draft,
      version: saved.version,
      groups: draft.groups.map((group) => ({
        ...group,
        ingredients: group.ingredients.map((one) =>
          one.id ? one : { ...one, id: claim(one.name) }
        )
      }))
    };
  }

  /**
   * The ingredient list, which the recipe keeps in its first group.
   *
   * Groups are the "for the dough" / "for the sauce" headings, and they stay
   * invisible until a recipe needs them, so the editor only ever writes to the
   * implicit first one.
   */
  const firstGroup = $derived(draft?.groups[0]?.ingredients ?? []);

  function setIngredients(ingredients: Ingredient[]) {
    const kept = new Set(ingredients.map((one) => one.id));
    const gone = new Set(firstGroup.filter((one) => !kept.has(one.id)).map((one) => one.id));

    // A deleted line comes off the steps that needed it too. The server refuses
    // a step needing an ingredient the recipe no longer has, and being told
    // that on the next autosave is no way to find out you deleted something.
    // `change` spreads its patch, so an absent key and one set to undefined are
    // not the same thing — the steps are only named when they have changed.
    change({
      groups: [{ id: draft?.groups[0]?.id ?? null, name: null, ingredients }],
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

  function change(patch: Partial<Recipe>) {
    if (!draft) {
      return;
    }

    draft = { ...draft, ...patch };
    recovered = false;

    // Written here first, synchronously, before anything is sent. The gap
    // between a keystroke and a save is where work goes missing.
    if (session.user) {
      remember(session.user.userId, recipeId, draft);
      unsent = true;
    }

    autosave.touch();
  }

  /**
   * What the small word beside the title says.
   *
   * In the order that matters. A conflict is never masked by anything
   * reassuring; work that has not reached the server never reads as "Saved";
   * and a lost connection reads as where the work is rather than as a failure,
   * because the work is not lost — it is on this device, and saying "Could not
   * save" about it is both alarming and untrue.
   */
  const status = $derived.by(() => {
    if (autosave.failure && changedElsewhere(autosave.failure)) {
      return m['editor.changedElsewhere']();
    }

    if (autosave.state === 'saving') {
      return m['editor.saving']();
    }

    if (autosave.failure && !unreachable(autosave.failure)) {
      return m['editor.saveFailed']();
    }

    if (recovered) {
      return m['editor.recovered']();
    }

    if (unsent) {
      return m['editor.keptHere']();
    }

    return autosave.state === 'saved' ? m['editor.saved']() : '';
  });

  /** A failure that means the server was not reached, rather than refused. */
  const unreachable = (failure: AppError) =>
    failure.code === ErrorCodes.offline || failure.code === ErrorCodes.timeout;

  const conflicted = $derived(autosave.failure !== null && changedElsewhere(autosave.failure));

  /**
   * Two ways out of a conflict, and no third.
   *
   * Merging two people's recipes automatically is a guess, and a guess about
   * somebody's dinner is worse than a question. So the choice is theirs — and
   * it has to be offered, because the journal keeps what was typed and would
   * otherwise show it again on every reload, conflicting again forever.
   */
  async function keepMine() {
    const mine = draft;

    if (!mine) {
      return;
    }

    // Re-read to learn the version somebody else's change produced, then write
    // this text on top of it. Nothing of theirs is silently kept: they were
    // told to look, and this is the person looking.
    await recipes.load(recipeId);

    const latest = recipes.detail;

    if (!latest || latest.id !== recipeId) {
      return;
    }

    draft = { ...mine, version: latest.version };
    autosave.clear();

    await autosave.flush();
  }

  async function takeTheirs() {
    if (session.user) {
      forget(session.user.userId, recipeId);
    }

    await recipes.load(recipeId);

    const latest = recipes.detail;

    if (!latest || latest.id !== recipeId) {
      return;
    }

    // Assigned here rather than left to the effect above, which deliberately
    // takes the recipe only once: it is what stops a save in flight from
    // overwriting what is being typed, and it would leave this showing the
    // version the conflict was about.
    draft = latest;
    unsent = false;
    recovered = false;
    autosave.clear();
  }
</script>

<svelte:head><title>{draft?.title ?? m['editor.new']()}</title></svelte:head>

<Page width="reading">
  {#if draft}
    <!-- Bound once so the snippets below, which are separate closures, can see
         that it is there. -->
    {@const current = draft}

    <header class="head">
      <!-- Every page says what it is. This one's title is a text field that can
           be empty and can change while it is read aloud, so the heading is a
           sentence about the page rather than the field's value. Not shown:
           the field is right there, and printing the title twice above itself
           is how a screen fills up with things nobody asked for. -->
      <h1 class="ds-clipped">{m['editor.heading']({ title: current.title })}</h1>

      <a class="back" href={resolve('/(app)/recipes/[recipeId]', { recipeId })}>
        ← {m['editor.done']()}
      </a>

      <!-- Polite, because it reports something that already happened and must
           not interrupt whatever is being typed. -->
      <p class="status" role="status">{status}</p>

      {#if conflicted}
        <div class="conflict">
          <Button onclick={keepMine}>{m['editor.conflict.keepMine']()}</Button>
          <Button variant="ghost" onclick={takeTheirs}>
            {m['editor.conflict.takeTheirs']()}
          </Button>
        </div>
      {/if}
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

        <!-- Placeholdered with the word the recipe would use anyway, which is
             the whole explanation of what this field is for: it is already
             showing the answer, and typing over it is how you change it. -->
        <Field
          label={m['editor.yieldLabel']()}
          hint={m['editor.yieldLabelHint']()}
          optionalText={m['editor.optional']()}
        >
          {#snippet children({ id, describedBy, invalid })}
            <TextInput
              {id}
              {describedBy}
              {invalid}
              maxlength={40}
              placeholder={yieldNoun({ yieldKind: current.yieldKind, yieldLabel: null })}
              value={current.yieldLabel ?? ''}
              oninput={(value) => change({ yieldLabel: value.trim() ? value : null })}
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
          ingredients={firstGroup}
          steps={current.steps}
          onchange={setIngredients}
          householdId={current.householdId}
          language={current.language}
        />
      </section>

      <section>
        <h2 class="section">{m['editor.steps']()}</h2>

        <StepEditor
          steps={current.steps}
          ingredients={current.groups.flatMap((group) => group.ingredients)}
          onchange={(steps: Step[]) => change({ steps })}
          onaddingredient={addIngredient}
        />
      </section>
    </div>
  {/if}
</Page>

<style>
  .head {
    display: flex;
    flex-wrap: wrap;
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

  .conflict {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  .status {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .form {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
  }

  .numbers {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(min(10rem, 100%), 1fr));
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
