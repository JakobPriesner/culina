<script lang="ts">
  import IngredientEditor from '../IngredientEditor.svelte';
  import type { Ingredient, Step } from '../../types';

  /** Holds the list the way the edit page does: the parent owns it. */
  interface Props {
    ingredients?: Ingredient[];
    /** The method, so a row can say which steps its ingredient ends up in. */
    steps?: Step[];
    onchange?: (ingredients: Ingredient[]) => void;
  }

  let { ingredients = $bindable([]), steps = [], onchange = () => {} }: Props = $props();

  function change(next: Ingredient[]) {
    ingredients = next;
    onchange(next);
  }
</script>

<IngredientEditor {ingredients} {steps} onchange={change} householdId="h1" language="en" />
