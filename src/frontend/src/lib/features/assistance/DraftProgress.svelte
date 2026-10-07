<script lang="ts">
  import { GenerationStatus } from '$ds';
  import Olli from '$shell/olli/Olli.svelte';

  /** The assistant at work: a status line plus Olli, thinking before the first words, writing as they arrive; motion follows the device setting. */
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
