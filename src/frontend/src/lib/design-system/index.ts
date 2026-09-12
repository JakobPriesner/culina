/**
 * The design system's public surface.
 *
 * Features import from `$ds`, never from a file path inside it, so a component
 * can be moved or split without touching a page. Nothing here knows what a
 * recipe is — if it did, it would belong in `$features`.
 */
export { default as Button } from './actions/Button.svelte';
export { default as IconButton } from './actions/IconButton.svelte';

export { default as Checkbox } from './inputs/Checkbox.svelte';
export { default as Field } from './inputs/Field.svelte';
export { default as RadioGroup } from './inputs/RadioGroup.svelte';
export { default as SearchField } from './inputs/SearchField.svelte';
export { default as Select } from './inputs/Select.svelte';
export { default as Stepper } from './inputs/Stepper.svelte';
export { default as Switch } from './inputs/Switch.svelte';
export { default as TextArea } from './inputs/TextArea.svelte';
export { default as TextInput } from './inputs/TextInput.svelte';
