<script lang="ts">
  import Switch from '../inputs/Switch.svelte';

  interface Props {
    checked?: boolean;
    description?: string;
    onchange?: (checked: boolean) => void;
  }

  let { checked = $bindable(false), ...rest }: Props = $props();
</script>

<Switch label="Open registration" bind:checked {...rest} />
