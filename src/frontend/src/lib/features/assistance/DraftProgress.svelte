<script lang="ts">
  import { GenerationStatus } from '$ds';
  import Olla from '$shell/olla/Olla.svelte';

  /**
   * The assistant at work, said with the status line and shown with Olla.
   *
   * Olla reads along while the request is out, and leans in to watch once the
   * first words arrive. It watches; it does not write. The four-colour glow
   * around the draft stays the one sign of what a machine made, and the status
   * line is still the thing a screen reader hears.
   *
   * Small, beside the line, and still between changes: somebody is reading
   * the draft as it lands, and a mascot that kept moving would be competing
   * with it. With Olla turned off, the status line stands alone, as it always
   * has.
   */
  interface Props {
    label: string;
    /** Whether any of the draft has arrived yet. */
    arriving: boolean;
  }

  let { label, arriving }: Props = $props();
</script>

<div class="progress">
  <Olla pose={arriving ? 'watching' : 'reading'} size="sm" />
  <GenerationStatus {label} />
</div>

<style>
  .progress {
    display: flex;
    align-items: center;
    gap: var(--space-2);
  }
</style>
