<script lang="ts">
  interface Props {
    label: string;
  }

  let { label }: Props = $props();
</script>

<!-- Covers the photo too: dropping onto it replaces it. -->
<div class="drop" aria-hidden="true">
  <p class="drop-label">{label}</p>
</div>

<style>
  /* Dashed accent outline like the empty template. Not a drop target itself (`pointer-events: none` keeps the frame's events) and opaque so what it covers does not show through. */
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
