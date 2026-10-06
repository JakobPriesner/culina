<script lang="ts">
  import { GenerationStatus } from '$ds';
  import Olli from '$shell/olli/Olli.svelte';

  /**
   * The assistant at work, said with the status line and shown with Olli.
   *
   * A legible work scene: thinking before the first words, writing as they
   * arrive. The status remains plain text and motion follows the device setting without
   * interrupting the request.
   */
  interface Props {
    label: string;
    /** Whether any of the draft has arrived yet. */
    arriving: boolean;
  }

  let { label, arriving }: Props = $props();
</script>

<div class="progress">
  <div class="mascot">
    <Olli pose={arriving ? 'writing' : 'thinking'} size="md" working />
  </div>
  <div class="copy">
    <GenerationStatus {label} indicator={false} />
  </div>
</div>

<style>
  .progress {
    display: flex;
    align-items: center;
    gap: var(--space-4);
    min-width: 0;
  }

  .mascot {
    flex: none;
  }

  .mascot:empty {
    display: none;
  }

  .copy {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-1);
    min-width: 0;
  }

  @media (width < 30rem) {
    .mascot :global(.olli) {
      width: 6rem;
      height: 6rem;
    }
    .progress {
      gap: var(--space-2);
    }
  }
</style>
