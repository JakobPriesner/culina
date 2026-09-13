<script lang="ts">
  import { Checkbox, Stepper } from '$ds';
  import { formatNumber, m } from '$shell/i18n';
  import type { PreviewProgress, PreviewRecipe } from './recipes';

  let {
    recipe,
    progress,
    onprogress
  }: {
    recipe: PreviewRecipe;
    progress: PreviewProgress;
    onprogress: (value: PreviewProgress) => void;
  } = $props();
  const id = $props.id();
</script>

<div class="servings">
  <span>{m['preview.servings']()}</span>
  <Stepper
    id={`${id}-servings`}
    label={m['preview.servings']()}
    decreaseLabel={m['preview.less']()}
    increaseLabel={m['preview.more']()}
    value={progress.servings}
    onchange={(servings) => onprogress({ ...progress, servings })}
    min={1}
    max={12}
  />
</div>
<ul>
  {#each recipe.ingredients as ingredient (ingredient.name)}
    <li>
      <Checkbox
        checked={progress.checked[ingredient.name] ?? false}
        label={ingredient.name}
        onchange={(checked) =>
          onprogress({ ...progress, checked: { ...progress.checked, [ingredient.name]: checked } })}
      />
      <span class="amount" class:checked={progress.checked[ingredient.name]}>
        {formatNumber((ingredient.quantity * progress.servings) / 2, { maximumFractionDigits: 2 })}
        {ingredient.unit}
      </span>
    </li>
  {/each}
</ul>
<p class="note">{m['preview.scalingNote']()}</p>

<style>
  .servings {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-4);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
  ul {
    list-style: none;
    padding: 0;
    margin-block: var(--space-4);
  }
  li {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-4);
    min-height: var(--control-lg);
    border-bottom: 1px solid var(--border);
  }
  .amount {
    white-space: nowrap;
    font-variant-numeric: tabular-nums;
    font-weight: var(--weight-medium);
  }
  .checked {
    text-decoration: line-through;
    color: var(--text-muted);
  }
  .note {
    font-size: var(--text-xs);
    color: var(--text-muted);
    max-width: 40ch;
    line-height: var(--leading-relaxed);
  }
</style>
