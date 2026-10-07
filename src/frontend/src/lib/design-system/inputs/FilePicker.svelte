<script lang="ts">
  /**
   * The hidden file input opened by a `Button` the caller draws (`picker.open()`), since a native file input cannot be styled.
   * Clipped rather than `display: none` to stay in the accessibility tree; `tabindex="-1"` because it is driven from elsewhere.
   */
  interface Props {
    label: string;
    accept?: string;
    /** The chosen file. Never null: nothing is reported unless one was picked. */
    onpick: (file: File) => void;
  }

  let { label, accept, onpick }: Props = $props();

  let input = $state<HTMLInputElement>();

  export function open() {
    input?.click();
  }

  function chosen(event: Event) {
    const element = event.currentTarget as HTMLInputElement;
    const file = element.files?.[0];

    // Cleared so choosing the same file twice still fires (no value change otherwise).
    element.value = '';

    if (file) {
      onpick(file);
    }
  }
</script>

<input
  bind:this={input}
  class="ds-clipped"
  type="file"
  {accept}
  tabindex="-1"
  aria-label={label}
  onchange={chosen}
/>
