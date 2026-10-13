<script lang="ts">
  import { untrack } from 'svelte';

  import type { AppError } from '$api';
  import { Button, Field, TextInput } from '$ds';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';

  import type { Label } from './stores/householdFoods.svelte';

  /**
   * A food typed from a package's label, per 100 g, for what the table does not have. Only the name and the
   * kilocalories are needed; a value left empty is unknown, never zero. The server checks the label adds up and
   * says why when it does not.
   */
  interface Props {
    /** What the field starts with: the ingredient as written. */
    name: string;
    onsave: (label: Label) => Promise<AppError | null>;
    oncancel: () => void;
  }

  let { name, onsave, oncancel }: Props = $props();

  const parts = [
    'energyKcal',
    'energyKj',
    'fat',
    'saturatedFat',
    'carbohydrate',
    'sugars',
    'protein',
    'salt'
  ] as const;

  type Part = (typeof parts)[number];

  const labels: Record<Part, () => string> = {
    energyKcal: () => m['nutrition.package.energyKcal'](),
    energyKj: () => m['nutrition.package.energyKj'](),
    fat: () => m['nutrition.package.fat'](),
    saturatedFat: () => m['nutrition.package.saturatedFat'](),
    carbohydrate: () => m['nutrition.package.carbohydrate'](),
    sugars: () => m['nutrition.package.sugars'](),
    protein: () => m['nutrition.package.protein'](),
    salt: () => m['nutrition.package.salt']()
  };

  let typedName = $state(untrack(() => name));
  let typed = $state<Record<Part, string>>({
    energyKcal: '',
    energyKj: '',
    fat: '',
    saturatedFat: '',
    carbohydrate: '',
    sugars: '',
    protein: '',
    salt: ''
  });
  let saving = $state(false);
  let failure = $state<string | null>(null);

  /** A number as people write it, "7,5" too; empty is unknown. */
  const numberOf = (text: string): number | null => {
    const value = Number(text.trim().replace(',', '.'));

    return text.trim() && Number.isFinite(value) ? value : null;
  };

  async function submit(event: SubmitEvent) {
    event.preventDefault();

    const kcal = numberOf(typed.energyKcal);

    if (!typedName.trim() || kcal === null) {
      failure = m['nutrition.package.needed']();

      return;
    }

    saving = true;

    const error = await onsave({
      name: typedName.trim(),
      energyKcal: kcal,
      energyKj: numberOf(typed.energyKj),
      fat: numberOf(typed.fat),
      saturatedFat: numberOf(typed.saturatedFat),
      carbohydrate: numberOf(typed.carbohydrate),
      sugars: numberOf(typed.sugars),
      protein: numberOf(typed.protein),
      salt: numberOf(typed.salt)
    });

    saving = false;
    failure = error ? explain(error) : null;
  }
</script>

<form class="form" onsubmit={submit} novalidate>
  <p class="quiet">{m['nutrition.package.reach']()}</p>

  <Field label={m['nutrition.package.name']()}>
    {#snippet children({ id, describedBy, invalid })}
      <TextInput
        {id}
        {describedBy}
        {invalid}
        bind:value={typedName}
        autocomplete="off"
        maxlength={120}
      />
    {/snippet}
  </Field>

  <div class="values">
    {#each parts as part (part)}
      <Field label={labels[part]()}>
        {#snippet children({ id, describedBy, invalid })}
          <TextInput
            {id}
            {describedBy}
            {invalid}
            bind:value={typed[part]}
            inputmode="decimal"
            autocomplete="off"
          />
        {/snippet}
      </Field>
    {/each}
  </div>

  {#if failure}
    <p class="failure" role="alert">{failure}</p>
  {/if}

  <div class="actions">
    <Button type="submit" variant="primary" loading={saving}>
      {m['nutrition.package.save']()}
    </Button>
    <Button onclick={oncancel}>{m['nutrition.package.cancel']()}</Button>
  </div>
</form>

<style>
  .form {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    min-width: 0;
  }

  .quiet {
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .values {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(9rem, 1fr));
    align-items: end;
    gap: var(--space-3);
  }

  .failure {
    margin: 0;
    color: var(--text-danger);
    font-size: var(--text-sm);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }
</style>
