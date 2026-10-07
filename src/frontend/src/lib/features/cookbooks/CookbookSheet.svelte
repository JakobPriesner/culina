<script lang="ts">
  import { Button, Field, RadioGroup, Sheet, TextArea, TextInput } from '$ds';

  import { m } from '$shell/i18n';
  import RuleEditor from './RuleEditor.svelte';
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
    onsave: (name: string, description: string | null, rules: CookbookRules | null) => void;
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

  // Reset on every open and on the way out, so a reopened sheet never shows another cookbook's text.
  $effect(() => {
    if (open) {
      name = cookbook?.name ?? preset?.name ?? '';
      description = cookbook?.description ?? '';
      // A preset exists because a saved search asked for a self-filling shelf.
      smart = cookbook !== null ? cookbook.kind === 'smart' : preset !== null;
      rules = cookbook?.rules ?? preset?.rules ?? noRules;
    }
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
</style>
