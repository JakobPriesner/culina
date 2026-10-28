<script lang="ts">
  import { Button, ErrorState } from '$ds';
  import { session } from '$features/auth/session.svelte';
  import IngredientMergesPanel from '$features/ingredients/IngredientMergesPanel.svelte';
  import IngredientNamesPanel from '$features/ingredients/IngredientNamesPanel.svelte';
  import IngredientNounsPanel from '$features/ingredients/IngredientNounsPanel.svelte';
  import MergeSuggestionsPanel from '$features/ingredients/MergeSuggestionsPanel.svelte';
  import { ingredientNames } from '$features/ingredients/stores/ingredientNames.svelte';
  import HouseholdFoodsPanel from '$features/nutrition/HouseholdFoodsPanel.svelte';
  import IngredientFactsPanel from '$features/nutrition/IngredientFactsPanel.svelte';
  import { householdFoods } from '$features/nutrition/stores/householdFoods.svelte';
  import { nouns } from '$features/recipes/stores/nouns.svelte';
  import { ingredientFacts } from '$features/nutrition/stores/ingredientFacts.svelte';
  import { m } from '$shell/i18n';

  import SettingsSection from '../SettingsSection.svelte';

  /** The household's ingredient names, kept to one per ingredient, and what it said about them for nutrition. */
  const household = $derived(session.activeHousehold);

  function load(householdId: string) {
    void ingredientNames.load(householdId);
    void nouns.load(householdId);
    void householdFoods.load(householdId);
  }

  $effect(() => {
    if (household) {
      load(household.householdId);
    }
  });

  /**
   * The names and the nutrition choices come from two separate reads: when both fail the server is out of reach, so
   * the page says it once with one retry, rather than every section repeating it under its heading.
   */
  const unreachable = $derived(
    household !== null &&
      ingredientNames.status(household.householdId) === 'failed' &&
      ingredientFacts.status(household.householdId) === 'failed'
  );

  function retry(householdId: string) {
    load(householdId);
    void ingredientFacts.load(householdId);
  }

  // A merge moves the nutrition choices made for the merged name, so that list is read again.
  const merged = () => household && void ingredientFacts.load(household.householdId);
</script>

<svelte:head><title>{m['me.ingredients']()}</title></svelte:head>

{#if household && unreachable}
  <ErrorState title={m['ingredients.failed']()} body={m['error.unexpected.body']()}>
    {#snippet action()}
      <Button onclick={() => retry(household.householdId)}>{m['error.retry']()}</Button>
    {/snippet}
  </ErrorState>
{:else if household}
  <SettingsSection
    title={m['ingredients.suggestions.title']()}
    description={m['ingredients.suggestions.body']()}
    bare
  >
    <MergeSuggestionsPanel householdId={household.householdId} onmerged={merged} />
  </SettingsSection>

  <SettingsSection
    title={m['ingredients.names.title']()}
    description={m['ingredients.names.body']()}
    bare
  >
    <IngredientNamesPanel householdId={household.householdId} onmerged={merged} />
  </SettingsSection>

  <SettingsSection
    title={m['ingredients.merges.title']()}
    description={m['ingredients.merges.body']()}
    bare
  >
    <IngredientMergesPanel householdId={household.householdId} />
  </SettingsSection>

  <SettingsSection
    title={m['ingredients.nouns.title']()}
    description={m['ingredients.nouns.body']()}
    bare
  >
    <IngredientNounsPanel householdId={household.householdId} />
  </SettingsSection>

  <SettingsSection
    title={m['ingredients.nutrition.title']()}
    description={m['household.ingredients.body']()}
    bare
  >
    <IngredientFactsPanel householdId={household.householdId} />
  </SettingsSection>

  <SettingsSection
    title={m['nutrition.package.listTitle']()}
    description={m['nutrition.package.listBody']()}
    bare
  >
    <HouseholdFoodsPanel householdId={household.householdId} onremoved={merged} />
  </SettingsSection>
{:else}
  <p class="none">{m['me.household.none']()}</p>
{/if}

<style>
  .none {
    max-width: var(--measure);
    color: var(--text-muted);
  }
</style>
