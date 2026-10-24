<script lang="ts" module>
  /** Figures the person typed for something that cannot be looked up; unknown macros stay unknown. */
  export interface TypedFigures {
    readonly label: string;
    readonly kcal: number;
    readonly protein: number | null;
    readonly carbohydrate: number | null;
    readonly fat: number | null;
  }

  /** The fields as typed, before they are read as numbers. */
  export interface OwnFiguresText {
    label: string;
    kcal: string;
    protein: string;
    carbohydrate: string;
    fat: string;
  }

  const number = (text: string): number | null => {
    const parsed = Number(text.replace(',', '.'));

    return text.trim() === '' || !Number.isFinite(parsed) ? null : parsed;
  };

  const text = (value: number | null | undefined): string => (value == null ? '' : String(value));

  export const noFigures = (): OwnFiguresText => ({
    label: '',
    kcal: '',
    protein: '',
    carbohydrate: '',
    fat: ''
  });

  /** The fields filled with what was typed before, to correct it. */
  export const figuresOf = (
    label: string,
    figures: {
      kcal?: number | null;
      protein?: number | null;
      carbohydrate?: number | null;
      fat?: number | null;
    }
  ): OwnFiguresText => ({
    label,
    kcal: text(figures.kcal),
    protein: text(figures.protein),
    carbohydrate: text(figures.carbohydrate),
    fat: text(figures.fat)
  });

  /** What was typed, or null while it cannot be logged: it needs a name and plausible kilocalories. */
  export function typedFigures(fields: OwnFiguresText): TypedFigures | null {
    const kcal = number(fields.kcal);

    return fields.label.trim().length === 0 || kcal === null || kcal < 0 || kcal > 10000
      ? null
      : {
          label: fields.label.trim(),
          kcal,
          protein: number(fields.protein),
          carbohydrate: number(fields.carbohydrate),
          fat: number(fields.fat)
        };
  }
</script>

<script lang="ts">
  import { Field, TextInput } from '$ds';
  import { m } from '$shell/i18n';

  /**
   * The person's own figures for a café lunch: a name and kilocalories, and the macros they know. Shared by logging
   * them and by correcting them later.
   */
  let { fields = $bindable() }: { fields: OwnFiguresText } = $props();
</script>

<Field label={m['diary.add.ownLabel']()}>
  {#snippet children({ id, describedBy })}
    <TextInput
      {id}
      {describedBy}
      value={fields.label}
      maxlength={200}
      oninput={(value) => (fields.label = value)}
    />
  {/snippet}
</Field>
<Field label={m['diary.add.ownKcal']()}>
  {#snippet children({ id, describedBy })}
    <TextInput
      {id}
      {describedBy}
      value={fields.kcal}
      inputmode="decimal"
      oninput={(value) => (fields.kcal = value)}
    />
  {/snippet}
</Field>
<div class="macros">
  <Field label={m['diary.protein']()} optionalText={m['diary.optional']()}>
    {#snippet children({ id, describedBy })}
      <TextInput
        {id}
        {describedBy}
        value={fields.protein}
        inputmode="decimal"
        oninput={(value) => (fields.protein = value)}
      />
    {/snippet}
  </Field>
  <Field label={m['diary.carbohydrate']()} optionalText={m['diary.optional']()}>
    {#snippet children({ id, describedBy })}
      <TextInput
        {id}
        {describedBy}
        value={fields.carbohydrate}
        inputmode="decimal"
        oninput={(value) => (fields.carbohydrate = value)}
      />
    {/snippet}
  </Field>
  <Field label={m['diary.fat']()} optionalText={m['diary.optional']()}>
    {#snippet children({ id, describedBy })}
      <TextInput
        {id}
        {describedBy}
        value={fields.fat}
        inputmode="decimal"
        oninput={(value) => (fields.fat = value)}
      />
    {/snippet}
  </Field>
</div>

<style>
  .macros {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(7rem, 1fr));
    gap: var(--space-3);
  }
</style>
