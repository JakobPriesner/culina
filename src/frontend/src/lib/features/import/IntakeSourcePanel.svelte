<script lang="ts">
  import { Disclosure } from '$ds';
  import { m } from '$shell/i18n';
  import IntakeSourceMaterial from './IntakeSourceMaterial.svelte';
  import type { IntakeJob } from './intakes.svelte';

  /**
   * The original beside the editor, so a misread amount or a garbled caption can be fixed against what
   * was actually sent. Open from the start: this is what the cook came to compare with.
   */
  interface Props {
    job: IntakeJob;
  }

  let { job }: Props = $props();
</script>

<aside class="intake-source" aria-label={m['import.review.source']()}>
  <Disclosure summary={m['import.review.source']()} open>
    <IntakeSourceMaterial {job} />
  </Disclosure>
</aside>

<style>
  .intake-source {
    min-width: 0;
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-md);
    background: var(--surface-sunken);
  }

  /* Beside the form where there is room (see EditorLayout), scrolling on its own next to the line being fixed. */
  @media (min-width: 90rem) {
    .intake-source {
      max-height: calc(100dvh - var(--space-24) - var(--space-6));
      overflow-y: auto;
    }
  }
</style>
