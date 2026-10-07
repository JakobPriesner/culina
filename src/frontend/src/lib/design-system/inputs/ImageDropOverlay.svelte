<script lang="ts">
  /** What an image field's frame says while a file is held over it. */
  interface Props {
    label: string;
  }

  let { label }: Props = $props();
</script>

<!-- Over the picture as well as the template: dropping onto a photo
     replaces it, and the frame says where it will land either way. -->
<div class="drop" aria-hidden="true">
  <p class="drop-label">{label}</p>
</div>

<style>
  /*
   * Where a held file will land.
   *
   * The same dashed outline as the empty template, in the accent: the frame
   * saying "here" in the one convention it already uses for it. Not a target
   * itself — the frame underneath is — so it never swallows the events that
   * keep it on screen. Opaque, because whatever it covers — the template's
   * hint or the photo about to go — showing through it is a second sentence
   * under the one that matters.
   */
  .drop {
    position: absolute;
    z-index: 3;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    padding: var(--space-4);
    border: 2px dashed var(--accent);
    border-radius: var(--radius-lg);
    background: var(--surface-overlay);
    pointer-events: none;
  }

  .drop-label {
    color: var(--text);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    text-align: center;
  }
</style>
