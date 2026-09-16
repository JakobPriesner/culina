<script lang="ts">
  import { Button, Field, RadioGroup, Sheet, TextArea, TextInput } from '$ds';

  import { m } from '$shell/i18n';
  import RuleEditor from './RuleEditor.svelte';
  import type { CookbookDetail, CookbookRules } from './types';

  /**
   * Naming a cookbook, whether it is new or already exists.
   *
   * One sheet and one form for both, because they are the same two questions —
   * a second component for renaming would be a second place for the length
   * limits and the empty-name rule to drift apart.
   */
  interface Props {
    open: boolean;
    /** Whose tags the rule editor offers. */
    householdId: string;
    /** The cookbook being edited, or null when making a new one. */
    cookbook?: CookbookDetail | null;
    /** True while the write is in flight. */
    saving?: boolean;
    onsave: (name: string, description: string | null, rules: CookbookRules | null) => void;
    onclose: () => void;
  }

  let { open, householdId, cookbook = null, saving = false, onsave, onclose }: Props = $props();

  const noRules: CookbookRules = { tags: [], ingredients: [], maxMinutes: null };

  let name = $state('');
  let description = $state('');
  let smart = $state(false);
  let rules = $state<CookbookRules>(noRules);

  // Filled from whatever is being edited each time it opens, and emptied on the
  // way out, so a sheet reopened later never shows the last thing typed into
  // a different cookbook.
  $effect(() => {
    if (open) {
      name = cookbook?.name ?? '';
      description = cookbook?.description ?? '';
      smart = cookbook?.kind === 'smart';
      rules = cookbook?.rules ?? noRules;
    }
  });

  const editing = $derived(cookbook !== null);

  const statesARule = $derived(
    rules.tags.length > 0 || rules.ingredients.length > 0 || rules.maxMinutes !== null
  );

  // A shelf that asks for nothing is every recipe you have, which is the screen
  // it would be reached from — so the button stays out of reach until it asks
  // for something.
  const ready = $derived(name.trim().length > 0 && (!smart || statesARule));

  function save() {
    if (!ready || saving) {
      return;
    }

    onsave(name.trim(), description.trim() || null, smart ? rules : null);
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

    <!-- Asked once, when the cookbook is made. The two kinds answer "why is
         this recipe here?" differently, so a shelf that changed its mind would
         have two answers for the recipes already on it. -->
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

    <!-- Inside the form so Enter submits it, which is what a two-field form
         with one obvious answer should do. -->
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
</style>
