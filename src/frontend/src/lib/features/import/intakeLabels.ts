import { m } from '$shell/i18n';

/** What an intake stage is called to the person waiting on it. */
export function stageLabel(stage: string): string | undefined {
  const labels: Record<string, string> = {
    queued: m['intake.queued'](),
    reading: m['intake.reading'](),
    thinking: m['intake.thinking'](),
    writing: m['intake.writing'](),
    saving: m['intake.saving'](),
    ready: m['intake.ready'](),
    reviewed: m['intake.saved'](),
    failed: m['intake.failed']()
  };

  return labels[stage];
}
