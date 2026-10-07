import { intakes } from '$features/import/intakes.svelte';
import { sharedAddress } from '$features/import/sharedRecipe';
import { explain } from '$shell/explain';
import { m } from '$shell/i18n';
import { preferences } from '$shell/preferences.svelte';

import { createSubmissionKey } from './submissionKey';

export interface IntakeSource {
  householdId: string;
  text: string;
  transcript: string;
  /** The address already read from. */
  sourceUrl: string;
  /** The address field's content, possibly a link not yet read. */
  url: string;
  photos: File[];
}

/** Queues the pasted material as an assistant job; identical material reuses one submission key so a retry is recognised. */
export function createAssistedIntake() {
  const keyFor = createSubmissionKey();

  return async (source: IntakeSource): Promise<{ jobId: string } | { failure: string }> => {
    const address = source.sourceUrl || sharedAddress(source.url, source.text);

    const job = await intakes.start({
      id: keyFor([
        source.householdId,
        preferences.locale,
        source.text,
        source.transcript,
        source.sourceUrl,
        source.url,
        source.photos.map((photo) => [photo.name, photo.size, photo.lastModified])
      ]),
      householdId: source.householdId,
      language: preferences.locale,
      material: source.text,
      transcript: source.transcript,
      sourceUrl: address || undefined,
      photos: source.photos,
      fetchSource: !source.sourceUrl && Boolean(address)
    });

    if (!job) {
      return { failure: intakes.error ? explain(intakes.error) : m['intake.failed']() };
    }

    return { jobId: job.id };
  };
}
