<script lang="ts">
  import SearchField from '../inputs/SearchField.svelte';

  interface Props {
    value?: string;
    oninput?: (value: string) => void;
  }

  let { value = $bindable(''), ...rest }: Props = $props();
</script>

<SearchField
  id="search"
  label="Search recipes"
  clearLabel="Clear search"
  placeholder="Search recipes"
  bind:value
  {...rest}
/>
