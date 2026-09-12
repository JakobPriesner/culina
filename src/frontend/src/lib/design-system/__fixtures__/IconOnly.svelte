<script lang="ts">
  import IconButton from '../actions/IconButton.svelte';

  interface Props {
    label?: string;
    pressed?: boolean;
    disabled?: boolean;
    onclick?: (event: MouseEvent) => void;
  }

  let { label = 'Add to shopping list', ...rest }: Props = $props();
</script>

<IconButton {label} {...rest}>
  <svg viewBox="0 0 24 24"><path d="M12 5v14M5 12h14" /></svg>
</IconButton>
