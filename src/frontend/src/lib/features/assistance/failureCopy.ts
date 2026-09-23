import { m } from '$shell/i18n';

/** What to say about one assistant failure, and whether the fix is in settings. */
export interface FailureCopy {
  message: () => string;
  /** Whether the assistant settings are where this gets fixed. */
  settings: boolean;
}

const copy = (message: () => string, settings = false): FailureCopy => ({ message, settings });

/**
 * Every code the assistant can fail with, as the reader's language says it.
 *
 * Keyed by the stable code rather than read off the server's detail, which is
 * English and written for a log. Budgets count as settings problems because an
 * administrator can raise them; a busy provider or an unreadable answer does
 * not, because nothing there would help.
 */
const byCode: Record<string, FailureCopy> = {
  'assistance.not_configured': copy(() => m['assist.failure.not_configured'](), true),
  'assistance.disabled': copy(() => m['assist.failure.disabled'](), true),
  'assistance.model_missing': copy(() => m['assist.failure.model_missing'](), true),
  'assistance.unknown_provider': copy(() => m['assist.failure.unknown_provider'](), true),
  'assistance.drawing_not_supported': copy(() => m['assist.failure.drawing_not_supported'](), true),
  'assistance.unavailable': copy(() => m['assist.failure.unavailable'](), true),
  'assistance.rejected': copy(() => m['assist.failure.rejected'](), true),
  'assistance.throttled': copy(() => m['assist.failure.throttled']()),
  'assistance.budget_exhausted': copy(() => m['assist.failure.budget_exhausted'](), true),
  'assistance.personal_budget_exhausted': copy(
    () => m['assist.failure.personal_budget_exhausted'](),
    true
  ),
  'assistance.refused': copy(() => m['assist.failure.refused']()),
  'assistance.unusable_answer': copy(() => m['assist.failure.unusable_answer']()),
  'assistance.nothing_to_work_from': copy(() => m['assist.failure.nothing_to_work_from']()),
  'assistance.too_much_to_work_from': copy(() => m['assist.failure.too_much_to_work_from']())
};

/** The copy for an assistant failure, or null for one that is not the assistant's. */
export const failureCopy = (code: string): FailureCopy | null => byCode[code] ?? null;
