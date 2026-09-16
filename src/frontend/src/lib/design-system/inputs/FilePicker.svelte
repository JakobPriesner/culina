<script lang="ts">
  /**
   * The file input nobody sees, opened by a control that is not it.
   *
   * A browser's own file input cannot be styled and looks like nothing else in
   * the app, so every upload here is really a `Button` beside a clipped input.
   * That pairing was written out three times — a photo, an attempt, an archive
   * — under three class names and three comments saying the same thing, which
   * is three chances to clip it wrong.
   *
   * Clipped rather than `display: none`, which would take it out of the
   * accessibility tree as well, and `tabindex="-1"` because it is driven from
   * elsewhere: left in the tab order it is a stop with nothing to see.
   *
   * Opened imperatively — `picker.open()` — because the thing that opens it is
   * whatever the caller has already drawn.
   */
  interface Props {
    /** Names the input for anyone who reaches it anyway. */
    label: string;
    /** The `accept` list, e.g. `image/jpeg,image/png`. */
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

    // Cleared straight away so choosing the same file twice still fires: the
    // second choice is not a change of value, and without this it is silence.
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
