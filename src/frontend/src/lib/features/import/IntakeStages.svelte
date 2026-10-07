<script lang="ts">
  import { m } from '$shell/i18n';
  import { stageLabel } from './intakeLabels';
  import { stages } from './intakeStages';

  interface Props {
    /** Which stage is under way, as a position in `stages`. */
    step: number;
  }

  let { step }: Props = $props();
</script>

<ol class="stages" aria-label={m['intake.activity']()}>
  {#each stages as stage, index (stage)}
    <li class:complete={index < step} aria-current={index === step ? 'step' : undefined}>
      <span aria-hidden="true">{index < step ? '✓' : index + 1}</span>{stageLabel(stage)}
    </li>
  {/each}
</ol>

<style>
  .stages {
    list-style: none;
    padding: 0;
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2) var(--space-4);
    font-size: var(--text-xs);
    margin-block: var(--space-4);
    color: var(--text-muted);
  }
  .stages li {
    display: flex;
    align-items: center;
    gap: var(--space-1);
  }
  .stages span {
    flex: none;
    width: 1.5rem;
    height: 1.5rem;
    border: 1px solid var(--border);
    display: grid;
    place-items: center;
    border-radius: 50%;
    transition:
      background-color var(--duration-base) var(--ease-out),
      border-color var(--duration-base) var(--ease-out),
      color var(--duration-base) var(--ease-out);
  }
  .stages [aria-current] {
    color: var(--text);
    font-weight: var(--weight-medium);
  }
  .complete span {
    background: var(--surface-accent-subtle);
    color: var(--accent);
    border-color: var(--accent);
  }
  .stages [aria-current] span {
    background: var(--accent);
    border-color: var(--accent);
    color: var(--accent-contrast);
  }

  @media (width < 42rem) {
    .stages {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
    }
  }
</style>
