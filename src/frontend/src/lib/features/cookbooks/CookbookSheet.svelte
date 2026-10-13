<script lang="ts">
  import { Button, Field, RadioGroup, Sheet, TextArea, TextInput } from '$ds';

  import { imageUrl } from '$features/recipes/recipeImage';
  import { m } from '$shell/i18n';
  import RuleEditor from './RuleEditor.svelte';
  import { readShelfOrder, type ShelfLine } from './shelfOrder';
  import type { CookbookDetail, CookbookRules } from './types';

  /** Naming a cookbook, new or existing: one sheet and form so the length limits and empty-name rule cannot drift. */
  interface Props {
    open: boolean;
    householdId: string;
    /** The cookbook being edited, or null when making a new one. */
    cookbook?: CookbookDetail | null;
    /** Starting rules for a new cookbook (e.g. from a saved search); an existing cookbook's own rules always win. */
    preset?: { readonly name: string; readonly rules: CookbookRules } | null;
    saving?: boolean;
    /** The cover is passed only when editing: a new cookbook has nothing on it to choose from. */
    onsave: (
      name: string,
      description: string | null,
      rules: CookbookRules | null,
      cover?: string | null
    ) => void;
    onclose: () => void;
  }

  let {
    open,
    householdId,
    cookbook = null,
    preset = null,
    saving = false,
    onsave,
    onclose
  }: Props = $props();

  const noRules: CookbookRules = { tags: [], ingredients: [], maxMinutes: null };

  let name = $state('');
  let description = $state('');
  let smart = $state(false);
  let rules = $state<CookbookRules>(noRules);
  let cover = $state<string | null>(null);

  /** The photographed recipes on the cookbook being edited, which its cover can be. */
  let pictured = $state<readonly ShelfLine[]>([]);

  // Reset on every open and on the way out, so a reopened sheet never shows another cookbook's text.
  $effect(() => {
    if (open) {
      name = cookbook?.name ?? preset?.name ?? '';
      description = cookbook?.description ?? '';
      // A preset exists because a saved search asked for a self-filling shelf.
      smart = cookbook !== null ? cookbook.kind === 'smart' : preset !== null;
      rules = cookbook?.rules ?? preset?.rules ?? noRules;
      cover = cookbook?.coverRecipeId ?? null;
    }
  });

  // Read when an existing cookbook is opened for editing. Plain values only before the await, so the effect
  // does not depend on what it writes.
  $effect(() => {
    const id = open ? cookbook?.id : undefined;

    if (!id || !householdId) {
      pictured = [];

      return;
    }

    void readShelfOrder(householdId, id).then((read) => {
      pictured = read.ok ? read.lines.filter((line) => line.imageId !== null) : [];
    });
  });

  const editing = $derived(cookbook !== null);

  const statesARule = $derived(
    rules.tags.length > 0 || rules.ingredients.length > 0 || rules.maxMinutes !== null
  );

  // A shelf asking for nothing is every recipe, so the button stays disabled until it asks for something.
  const ready = $derived(name.trim().length > 0 && (!smart || statesARule));

  function save() {
    if (!ready || saving) {
      return;
    }

    if (editing) {
      onsave(name.trim(), description.trim() || null, smart ? rules : null, cover);
    } else {
      onsave(name.trim(), description.trim() || null, smart ? rules : null);
    }
  }
</script>

<Sheet
  {open}
  title={editing ? m['cookbooks.edit.title']() : m['cookbooks.new.title']()}
  closeLabel={m['cookbooks.add.done']()}
  {onclose}
>
  <form
    class="form"
    onsubmit={(event) => {
      event.preventDefault();
      save();
    }}
  >
    <Field label={m['cookbooks.field.name']()} required>
      {#snippet children({ id, describedBy, invalid })}
        <TextInput
          {id}
          {describedBy}
          {invalid}
          bind:value={name}
          placeholder={m['cookbooks.field.namePlaceholder']()}
          maxlength={80}
          required
        />
      {/snippet}
    </Field>

    <Field label={m['cookbooks.field.description']()} hint={m['cookbooks.field.descriptionHint']()}>
      {#snippet children({ id, describedBy, invalid })}
        <TextArea {id} {describedBy} {invalid} bind:value={description} maxlength={500} rows={3} />
      {/snippet}
    </Field>

    <!-- Asked once at creation: the two kinds answer "why is this recipe here?" differently, so changing kind would leave two answers. -->
    {#if !editing}
      <Field label={m['cookbooks.kind.question']()} group>
        {#snippet children({ describedBy })}
          <RadioGroup
            name="cookbook-kind"
            {describedBy}
            value={smart ? 'smart' : 'manual'}
            options={[
              { value: 'manual', label: m['cookbooks.kind.manual']() },
              { value: 'smart', label: m['cookbooks.kind.smart']() }
            ]}
            onchange={(value) => (smart = value === 'smart')}
          />
        {/snippet}
      </Field>
    {/if}

    {#if smart}
      <RuleEditor {householdId} {rules} onchange={(next) => (rules = next)} />

      {#if !statesARule}
        <p class="hint">{m['cookbooks.rules.none']()}</p>
      {/if}
    {/if}

    <!-- Only with something to choose from: one photo is already the whole cover. -->
    {#if editing && pictured.length > 1}
      <Field label={m['cookbooks.cover.label']()} hint={m['cookbooks.cover.hint']()} group>
        {#snippet children({ describedBy })}
          <div class="covers" aria-describedby={describedBy}>
            <label class="cover" class:chosen={cover === null}>
              <input
                class="ds-clipped"
                type="radio"
                name="cookbook-cover"
                checked={cover === null}
                onchange={() => (cover = null)}
              />
              <span class="mosaic" aria-hidden="true">
                {#each pictured.slice(0, 4) as line (line.id)}
                  <img src={imageUrl(line.id, 400, line.imageId)} alt="" loading="lazy" />
                {/each}
              </span>
              <span class="caption">{m['cookbooks.cover.automatic']()}</span>
            </label>

            {#each pictured as line (line.id)}
              <label class="cover" class:chosen={cover === line.id}>
                <input
                  class="ds-clipped"
                  type="radio"
                  name="cookbook-cover"
                  checked={cover === line.id}
                  onchange={() => (cover = line.id)}
                />
                <img src={imageUrl(line.id, 400, line.imageId)} alt="" loading="lazy" />
                <span class="caption">{line.title}</span>
              </label>
            {/each}
          </div>
        {/snippet}
      </Field>
    {/if}

    <!-- Inside the form so Enter submits it. -->
    <Button type="submit" variant="primary" disabled={!ready} loading={saving}>
      {editing ? m['cookbooks.edit.save']() : m['cookbooks.new.save']()}
    </Button>
  </form>
</Sheet>

<style>
  .form {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
  }

  .hint {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .covers {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(5.5rem, 1fr));
    gap: var(--space-3);
  }

  .cover {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 0;
    cursor: pointer;
  }

  .cover img,
  .mosaic {
    display: block;
    width: 100%;
    aspect-ratio: 1;
    object-fit: cover;
    border: 2px solid transparent;
    border-radius: var(--radius-md);
  }

  .mosaic {
    display: grid;
    grid-template-columns: 1fr 1fr;
    overflow: hidden;
    background: var(--surface-sunken);
  }

  .mosaic img {
    border: none;
    border-radius: 0;
  }

  /* A ring and a heavier caption, so the choice is not told by colour alone. */
  .chosen img,
  .chosen .mosaic {
    border-color: var(--accent);
  }

  .chosen .caption {
    color: var(--text);
    font-weight: var(--weight-semibold);
  }

  .cover:has(input:focus-visible) {
    outline: 2px solid var(--border-focus);
    outline-offset: 2px;
    border-radius: var(--radius-md);
  }

  .caption {
    overflow: hidden;
    color: var(--text-muted);
    font-size: var(--text-xs);
    text-overflow: ellipsis;
    white-space: nowrap;
  }
</style>
