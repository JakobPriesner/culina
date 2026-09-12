<script lang="ts">
  import Checkbox from '../inputs/Checkbox.svelte';
  import RadioGroup from '../inputs/RadioGroup.svelte';
  import Select from '../inputs/Select.svelte';
  import TextArea from '../inputs/TextArea.svelte';

  interface Props {
    indeterminate?: boolean;
    checkboxDisabled?: boolean;
    onchecked?: (checked: boolean) => void;
    oncourse?: (value: string) => void;
    onunit?: (value: string) => void;
  }

  let {
    indeterminate = false,
    checkboxDisabled = false,
    onchecked,
    oncourse,
    onunit
  }: Props = $props();

  let vegetarian = $state(false);
  let course = $state('main');
  let unit = $state('metric');
  let notes = $state('');
</script>

<Checkbox
  bind:checked={vegetarian}
  label="Vegetarian"
  {indeterminate}
  disabled={checkboxDisabled}
  onchange={onchecked}
/>

<RadioGroup
  name="course"
  bind:value={course}
  onchange={oncourse}
  options={[
    { value: 'starter', label: 'Starter' },
    { value: 'main', label: 'Main' },
    { value: 'dessert', label: 'Dessert', disabled: true }
  ]}
/>

<Select
  id="unit"
  bind:value={unit}
  onchange={onunit}
  options={[
    { value: 'metric', label: 'Metric' },
    { value: 'imperial', label: 'Imperial' }
  ]}
/>

<TextArea id="notes" bind:value={notes} />
