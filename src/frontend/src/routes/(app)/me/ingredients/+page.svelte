<script lang="ts">
  import { session } from '$features/auth/session.svelte';
  import IngredientMergesPanel from '$features/ingredients/IngredientMergesPanel.svelte';
  import IngredientNamesPanel from '$features/ingredients/IngredientNamesPanel.svelte';
  import MergeSuggestionsPanel from '$features/ingredients/MergeSuggestionsPanel.svelte';
  import { ingredientNames } from '$features/ingredients/stores/ingredientNames.svelte';
  import IngredientFactsPanel from '$features/nutrition/IngredientFactsPanel.svelte';
  import { ingredientFacts } from '$features/nutrition/stores/ingredientFacts.svelte';
  import { m } from '$shell/i18n';

  import SettingsSection from '../SettingsSection.svelte';

  /** The household's ingredient names, kept to one per ingredient, and what it said about them for nutrition. */
  const household = $derived(session.activeHousehold);

  $effect(() => {
    if (household) {
      void ingredientNames.load(household.householdId);
    }
  });

  // A merge moves the nutrition choices made for the merged name, so that list is read again.
  const merged = () => household && void ingredientFacts.load(household.householdId);
</script>

<svelte:head><title>{m['me.ingredients']()}</title></svelte:head>

{#if household}
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
    title={m['ingredients.nutrition.title']()}
    description={m['household.ingredients.body']()}
    bare
  >
    <IngredientFactsPanel householdId={household.householdId} />
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
