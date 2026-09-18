<script lang="ts">
  import { onDestroy } from 'svelte';

  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { Button, ErrorState, Field, Skeleton, TextArea, TextInput } from '$ds';
  import { createAutosave } from '$features/recipes/editor/autosave.svelte';
  import EditorRail from '$features/recipes/editor/EditorRail.svelte';
  import EditorSection from '$features/recipes/editor/EditorSection.svelte';
  import IngredientEditor from '$features/recipes/editor/IngredientEditor.svelte';
  import PhotoField from '$features/recipes/editor/PhotoField.svelte';
  import StepEditor from '$features/recipes/editor/StepEditor.svelte';
  import { withoutIngredients } from '$features/recipes/editor/stepUsage';
  import type { SaveTone } from '$features/recipes/editor/SaveState.svelte';
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

  /**
   * The numbers as they are being typed, before they are numbers.
   *
   * "", "1," and "0." are all things a half-typed amount looks like and none of
   * them survive a trip through `Number`. The old field read
   * `Number(value) || 1`, so clearing it to type "12" put a 1 back under the
   * cursor. Here the typed text is what the field shows until it parses, and
   * the recipe is only written to when it does.
   */
  let typed = $state<Record<'yieldAmount' | 'prepMinutes' | 'cookMinutes', string | undefined>>({
    yieldAmount: undefined,
    prepMinutes: undefined,
    cookMinutes: undefined
  });

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

  /** A number somebody typed, in either of the two ways Europe writes one. */
  const numberIn = (text: string): number | null => {
    const value = Number(text.replace(',', '.'));

    return text.trim() && Number.isFinite(value) ? value : null;
  };

  /**
   * How much it makes, which is the one number a recipe cannot do without.
   *
   * Every amount on the reading surface is derived from it, so a zero or a
   * word is refused rather than quietly turned into a 1: the field keeps what
   * was typed, says what is wrong with it, and the recipe keeps the last number
   * that made sense.
   */
  function writeYield(text: string) {
    typed = { ...typed, yieldAmount: text };

    const value = numberIn(text);

    if (value !== null && value > 0) {
      change({ yieldAmount: value });
    }
  }

  const yieldShown = $derived(typed.yieldAmount ?? String(draft?.yieldAmount ?? 1));
  const yieldWrong = $derived.by(() => {
    const value = numberIn(yieldShown);

    return value === null || value <= 0;
  });

  /** A time in minutes, which a recipe is allowed not to say. */
  function writeMinutes(which: 'prepMinutes' | 'cookMinutes', text: string) {
    typed = { ...typed, [which]: text };

    if (!text.trim()) {
      change({ [which]: null });

      return;
    }

    const value = numberIn(text);

    if (value !== null && value >= 0) {
      change({ [which]: Math.round(value) });
    }
  }

  const minutesShown = (which: 'prepMinutes' | 'cookMinutes') => {
    const written = typed[which];

    if (written !== undefined) {
      return written;
    }

    const saved = draft?.[which] ?? null;

    return saved === null ? '' : String(saved);
  };

  const minutesWrong = (text: string) => {
    if (!text.trim()) {
      return false;
    }

    const value = numberIn(text);

    return value === null || value < 0;
  };

  /**
   * What the two times add up to, said once and quietly.
   *
   * It is the number the library and the recipe's own header show, so seeing it
   * form while the two halves are typed is the difference between filling in
   * two fields and setting how long the recipe takes.
   */
  const totalMinutes = $derived.by(() => {
    const total = (draft?.prepMinutes ?? 0) + (draft?.cookMinutes ?? 0);

    return total > 0 ? total : null;
  });

  /**
   * What the small word beside the title says.
   *
   * In the order that matters. A conflict is never masked by anything
   * reassuring; work that has not reached the server never reads as "Saved";
   * and a lost connection reads as where the work is rather than as a failure,
   * because the work is not lost — it is on this device, and saying "Could not
   * save" about it is both alarming and untrue.
   *
   * At rest it says that the recipe saves itself, which is the one question an
   * editor with no Save button owes an answer to before anything has happened.
   */
  const saveState = $derived.by((): { tone: SaveTone; text: string } => {
    if (autosave.failure && changedElsewhere(autosave.failure)) {
      return { tone: 'conflict', text: m['editor.conflict.short']() };
    }

    if (autosave.state === 'saving') {
      return { tone: 'saving', text: m['editor.saving']() };
    }

    if (autosave.failure && !unreachable(autosave.failure)) {
      return { tone: 'failed', text: m['editor.saveFailed']() };
    }

    if (recovered) {
      return { tone: 'local', text: m['editor.recovered']() };
    }

    if (unsent) {
      return { tone: 'local', text: m['editor.keptHere']() };
    }

    return autosave.state === 'saved'
      ? { tone: 'saved', text: m['editor.saved']() }
      : { tone: 'idle', text: m['editor.savesItself']() };
  });

  /** A failure that means the server was not reached, rather than refused. */
  const unreachable = (failure: AppError) =>
    failure.code === ErrorCodes.offline || failure.code === ErrorCodes.timeout;

  const conflicted = $derived(autosave.failure !== null && changedElsewhere(autosave.failure));

  const back = $derived(resolve('/(app)/recipes/[recipeId]', { recipeId }));

  const sections = $derived([
    { id: 'recipe', label: m['editor.section.recipe']() },
    { id: 'photo', label: m['editor.photo']() },
    { id: 'ingredients', label: m['editor.ingredients'](), count: firstGroup.length },
    { id: 'steps', label: m['editor.steps'](), count: draft?.steps.length ?? 0 }
  ]);

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
    // Their numbers, not the ones this browser was in the middle of typing.
    typed = { yieldAmount: undefined, prepMinutes: undefined, cookMinutes: undefined };
    autosave.clear();
  }
</script>

<svelte:head><title>{draft?.title ?? m['editor.new']()}</title></svelte:head>

<Page>
  {#if draft}
    <!-- Bound once so the snippets below, which are separate closures, can see
         that it is there. -->
    {@const current = draft}

    <!-- Every page says what it is. This one's title is a text field that can
         be empty and can change while it is read aloud, so the heading is a
         sentence about the page rather than the field's value. Not shown: the
         field is right there, and printing the title twice above itself is how
         a screen fills up with things nobody asked for. -->
    <h1 class="ds-clipped">{m['editor.heading']({ title: current.title })}</h1>

    <div class="layout">
      <EditorRail
        backHref={back}
        backLabel={m['editor.done']()}
        title={current.title}
        untitled={m['editor.untitled']()}
        tone={saveState.tone}
        status={saveState.text}
        sectionsLabel={m['editor.sections']()}
        {sections}
      />

      <div class="form">
        {#if conflicted}
          <!--
            A banner in the form rather than two buttons in the corner: this is
            the only thing on the screen that needs a decision, and a decision
            offered in the chrome is one nobody sees.
          -->
          <div class="conflict" role="alert">
            <p class="conflict-title">{m['editor.conflict.title']()}</p>
            <p class="conflict-body">{m['editor.changedElsewhere']()}</p>

            <div class="conflict-actions">
              <Button variant="primary" onclick={keepMine}>
                {m['editor.conflict.keepMine']()}
              </Button>
              <Button onclick={takeTheirs}>{m['editor.conflict.takeTheirs']()}</Button>
            </div>
          </div>
        {/if}

        <EditorSection id="recipe" title={m['editor.section.recipe']()}>
          <div class="fields">
            <Field label={m['editor.title']()}>
              {#snippet children({ id, describedBy, invalid })}
                <TextInput
                  {id}
                  {describedBy}
                  {invalid}
                  size="display"
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

            <!--
              Two questions, four fields. "Makes / of what" is one thought and
              "hands-on / cooking" is another, and a single row of four
              equal-width boxes said neither — it said "here are four numbers,
              work it out".
            -->
            <div class="meta">
              <fieldset class="group">
                <legend class="legend">{m['editor.yieldGroup']()}</legend>

                <div class="pair yield">
                  <Field
                    label={m['editor.yieldAmount']()}
                    error={yieldWrong ? m['editor.yieldWrong']() : undefined}
                  >
                    {#snippet children({ id, describedBy, invalid })}
                      <TextInput
                        {id}
                        {describedBy}
                        {invalid}
                        inputmode="numeric"
                        value={yieldShown}
                        oninput={writeYield}
                      />
                    {/snippet}
                  </Field>

                  <!-- Placeholdered with the word the recipe would use anyway,
                       which is the whole explanation of what this field is for:
                       it is already showing the answer, and typing over it is
                       how you change it. -->
                  <Field label={m['editor.yieldLabel']()} optionalText={m['editor.optional']()}>
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
                </div>

                <p class="note">{m['editor.yieldLabelHint']()}</p>
              </fieldset>

              <fieldset class="group">
                <legend class="legend">{m['editor.timeGroup']()}</legend>

                <div class="pair">
                  <Field
                    label={m['editor.prepMinutes']()}
                    error={minutesWrong(minutesShown('prepMinutes'))
                      ? m['editor.minutesWrong']()
                      : undefined}
                  >
                    {#snippet children({ id, describedBy, invalid })}
                      <TextInput
                        {id}
                        {describedBy}
                        {invalid}
                        inputmode="numeric"
                        value={minutesShown('prepMinutes')}
                        oninput={(value) => writeMinutes('prepMinutes', value)}
                      />
                    {/snippet}
                  </Field>

                  <Field
                    label={m['editor.cookMinutes']()}
                    error={minutesWrong(minutesShown('cookMinutes'))
                      ? m['editor.minutesWrong']()
                      : undefined}
                  >
                    {#snippet children({ id, describedBy, invalid })}
                      <TextInput
                        {id}
                        {describedBy}
                        {invalid}
                        inputmode="numeric"
                        value={minutesShown('cookMinutes')}
                        oninput={(value) => writeMinutes('cookMinutes', value)}
                      />
                    {/snippet}
                  </Field>
                </div>

                <!-- The number the library and the recipe's own header show,
                     forming as the two halves are typed. -->
                <p class="note total" class:said={totalMinutes !== null}>
                  {totalMinutes === null
                    ? m['editor.minutesOptional']()
                    : m['editor.totalTime']({ count: totalMinutes })}
                </p>
              </fieldset>
            </div>
          </div>
        </EditorSection>

        <EditorSection id="photo" title={m['editor.photo']()}>
          <PhotoField
            {recipeId}
            imageId={current.imageId}
            onchange={(imageId) => {
              // The image is saved by its own endpoint, so this only keeps the
              // draft in step — touching autosave would write the recipe again
              // for a change it does not own.
              draft = draft ? { ...draft, imageId } : draft;
            }}
          />
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
            ingredients={current.groups.flatMap((group) => group.ingredients)}
            onchange={(steps: Step[]) => change({ steps })}
            onaddingredient={addIngredient}
          />
        </EditorSection>

        <!-- The end of the method is where somebody finishes writing, so it is
             where the way out belongs. The same words as the rail's quiet link
             above: one action, offered where each of the two hands is. -->
        <footer class="finish">
          <Button variant="primary" size="lg" href={back}>{m['editor.done']()}</Button>
        </footer>
      </div>
    </div>
  {:else if recipes.status === 'failed'}
    <ErrorState
      title={m['editor.failed.title']()}
      body={m['editor.failed.body']()}
      requestIdLabel={m['error.reference']()}
      requestId={recipes.error?.requestId}
    >
      {#snippet action()}
        <Button variant="primary" onclick={() => recipes.load(recipeId)}>
          {m['error.retry']()}
        </Button>
      {/snippet}
    </ErrorState>
  {:else}
    <!-- The shape of the editor, not a spinner: what is coming is a long form,
         and a skeleton the same shape means nothing moves when it lands. -->
    <div class="layout" aria-busy="true" aria-label={m['editor.loading']()}>
      <div class="ghost-rail">
        <Skeleton width="8rem" height="1.5rem" />
        <Skeleton width="100%" height="2rem" />
      </div>

      <div class="form">
        <div class="ghost">
          <Skeleton width="12rem" height="2rem" />
          <Skeleton width="100%" height="3.5rem" />
          <Skeleton width="100%" height="6rem" />
        </div>

        <div class="ghost">
          <Skeleton width="9rem" height="2rem" />
          <Skeleton width="100%" height="14rem" />
        </div>
      </div>
    </div>
  {/if}
</Page>

<style>
  /*
   * A rail and a column, which is the shape every other second-level screen in
   * this app has: the settings categories sit exactly here.
   */
  .layout {
    display: grid;
    gap: var(--space-6);
    align-items: start;
    min-width: 0;
  }

  .form {
    display: flex;
    flex-direction: column;
    gap: var(--layout-section-gap);
    min-width: 0;
    /*
     * Capped well short of the page. A recipe step is a sentence and an
     * ingredient is three words; set across a 1400px monitor they are unreadable
     * and the fields are absurd. The rail takes the width the form gives up.
     */
    max-width: 48rem;
  }

  .fields {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    min-width: 0;
  }

  .meta {
    display: grid;
    gap: var(--space-6);
    min-width: 0;
    margin-top: var(--space-2);
  }

  .group {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    min-width: 0;
    margin: 0;
    padding: 0;
    border: none;
  }

  /* The caption for a pair of fields, and the level between a section heading
     and a field label — so the three ranks of this form are three sizes rather
     than three shades of the same one. */
  .legend {
    padding: 0;
    color: var(--text-subtle);
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .pair {
    display: grid;
    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
    gap: var(--space-3);
    align-items: start;
    min-width: 0;
  }

  /* A count is one to three characters and a noun is a word: giving them equal
     columns was the reason "4" sat in a field as wide as "Portionen". */
  .yield {
    grid-template-columns: 6rem minmax(0, 1fr);
  }

  .note {
    color: var(--text-subtle);
    font-size: var(--text-xs);
    line-height: var(--leading-normal);
  }

  /* The total steps forward once there is one, rather than appearing out of
     nowhere where a hint was. */
  .total {
    transition: color var(--duration-base) var(--ease-out);
  }

  .total.said {
    color: var(--text-muted);
    font-variant-numeric: tabular-nums;
  }

  .conflict {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    padding: var(--space-4);
    border: 1px solid var(--warning);
    border-radius: var(--radius-lg);
    background: var(--warning-subtle);
    color: var(--text);
  }

  .conflict-title {
    font-weight: var(--weight-semibold);
  }

  .conflict-body {
    max-width: var(--measure);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  .conflict-actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
    margin-top: var(--space-2);
  }

  .finish {
    display: flex;
    justify-content: flex-start;
    padding-top: var(--space-4);
    border-top: 1px solid var(--border);
  }

  .ghost,
  .ghost-rail {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    min-width: 0;
  }

  .ghost + .ghost {
    margin-top: var(--layout-section-gap);
  }

  @media (min-width: 64rem) {
    .layout {
      grid-template-columns: 14rem minmax(0, 1fr);
      gap: var(--space-12);
    }

    .meta {
      grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
    }
  }
</style>
