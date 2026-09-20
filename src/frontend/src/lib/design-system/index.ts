/**
 * The design system's public surface.
 *
 * Features import from `$ds`, never from a file path inside it, so a component
 * can be moved or split without touching a page. Nothing here knows what a
 * recipe is — if it did, it would belong in `$features`.
 */
export { default as FilterChip } from './actions/FilterChip.svelte';
export { default as Button, type ButtonVariant } from './actions/Button.svelte';
export { default as IconButton } from './actions/IconButton.svelte';

export { default as Card } from './containment/Card.svelte';
export { default as Disclosure } from './containment/Disclosure.svelte';
export { default as Divider } from './containment/Divider.svelte';
export { default as Tabs, type Tab } from './containment/Tabs.svelte';

export { default as Avatar } from './display/Avatar.svelte';
export { default as Badge } from './display/Badge.svelte';
export { default as Icon } from './display/Icon.svelte';
export { default as Image } from './display/Image.svelte';
export { default as VisuallyHidden } from './display/VisuallyHidden.svelte';

export { default as Modal } from './overlay/Modal.svelte';
export { default as Popover } from './overlay/Popover.svelte';
export { default as Sheet } from './overlay/Sheet.svelte';

export { default as BusyRegion } from './feedback/BusyRegion.svelte';
export { default as ProgressBar } from './feedback/ProgressBar.svelte';
export { default as Toaster } from './feedback/Toaster.svelte';
export { default as EmptyState } from './feedback/EmptyState.svelte';
export { default as ErrorState } from './feedback/ErrorState.svelte';
export { default as Skeleton } from './feedback/Skeleton.svelte';

export { default as Checkbox } from './inputs/Checkbox.svelte';
export { default as Field } from './inputs/Field.svelte';
export { default as FilePicker } from './inputs/FilePicker.svelte';
export { default as ImageField } from './inputs/ImageField.svelte';
export { default as RadioGroup, type RadioOption } from './inputs/RadioGroup.svelte';
export { default as SearchField } from './inputs/SearchField.svelte';
export { default as SegmentedControl, type Segment } from './inputs/SegmentedControl.svelte';
export { default as Select } from './inputs/Select.svelte';
export { default as Stepper } from './inputs/Stepper.svelte';
export { default as Switch } from './inputs/Switch.svelte';
export { default as TextArea } from './inputs/TextArea.svelte';
export { default as TextInput } from './inputs/TextInput.svelte';
