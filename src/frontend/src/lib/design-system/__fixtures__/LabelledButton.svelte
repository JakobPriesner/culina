<script lang="ts">
  import Button, { type ButtonVariant } from '../actions/Button.svelte';

  /** Renders a Button with a label, since a snippet cannot be passed from a test. */
  interface Props {
    label?: string;
    variant?: ButtonVariant;
    loading?: boolean;
    disabled?: boolean;
    href?: string;
    onclick?: (event: MouseEvent) => void;
  }

  let { label = 'Save', ...rest }: Props = $props();
</script>

<Button {...rest}>{label}</Button>
