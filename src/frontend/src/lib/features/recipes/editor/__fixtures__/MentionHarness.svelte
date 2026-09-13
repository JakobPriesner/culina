<script lang="ts">
  import MentionField from '../MentionField.svelte';
  import type { Ingredient } from '../../types';

  /** Holds the value the way the edit page does: the parent owns it. */
  interface Props {
    value?: string;
    ingredients?: Ingredient[];
    onadd?: (name: string) => void;
  }

  let {
    value = $bindable(''),
    ingredients = $bindable([
      { id: 'i-butter', name: 'butter', note: null, quantity: { value: 200, unit: 'g' } },
      { id: 'i-oil', name: 'olive oil', note: null, quantity: { value: 2, unit: 'tbsp' } }
    ]),
    onadd = () => {}
  }: Props = $props();

  /** The edit page really does put the name on the list, so this does too. */
  function add(name: string) {
    ingredients = [
      ...ingredients,
      { id: `i-${name}`, name, note: null, quantity: { value: null, unit: null } }
    ];
    onadd(name);
  }
</script>

<MentionField
  id="step-0"
  label="Step 1"
  {value}
  {ingredients}
  oninput={(next) => (value = next)}
  onadd={add}
/>
