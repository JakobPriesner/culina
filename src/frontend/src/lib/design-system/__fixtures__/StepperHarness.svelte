<script lang="ts">
  import Stepper from '../inputs/Stepper.svelte';

  interface Props {
    value?: number;
    min?: number;
    max?: number;
    onchange?: (value: number) => void;
  }

  let { value = $bindable(4), ...rest }: Props = $props();
</script>

<Stepper
  id="servings"
  label="Servings"
  decreaseLabel="One fewer serving"
  increaseLabel="One more serving"
  bind:value
  {...rest}
/>
