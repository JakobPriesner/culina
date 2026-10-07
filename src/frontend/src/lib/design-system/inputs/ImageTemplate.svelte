<script lang="ts">
  /**
   * The empty state of an image field: the box the picture will occupy, drawn
   * rather than left blank.
   */
  interface Props {
    /** One line, under the outline. */
    hint: string;
    ratio: number;
  }

  let { hint, ratio }: Props = $props();
</script>

<!-- The same box the picture will occupy, drawn rather than left blank:
     an empty field that shows its own dimensions is a form saying what it
     wants, and a form that does not jump when it gets it. -->
<div class="template" style:aspect-ratio={ratio}>
  <svg
    class="glyph"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="1.5"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
  >
    <rect x="3" y="5" width="18" height="14" rx="2" />
    <circle cx="8.5" cy="10" r="1.5" />
    <!-- A horizon rising out of the frame: the shape of a photograph
         rather than of a broken one. -->
    <path d="m4 17 5-5 4 4 3-2 4 3" />
  </svg>

  <p class="hint">{hint}</p>
</div>

<style>
  .template {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: var(--space-2);
    padding: var(--space-4);
    /* Dashed, which is the one border convention everyone already reads as
       "this is where something goes". */
    border: 1px dashed var(--border-strong);
    border-radius: var(--radius-lg);
    background: var(--surface-sunken);
    color: var(--text-muted);
    text-align: center;
  }

  .glyph {
    width: var(--space-8);
    height: var(--space-8);
  }

  .hint {
    max-width: var(--measure);
    font-size: var(--text-sm);
  }
</style>
