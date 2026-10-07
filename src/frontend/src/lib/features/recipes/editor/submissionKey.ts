/**
 * An id for a submission that stays the same while what is submitted does.
 *
 * Sending the same material twice — a second tap after a failure — has to be
 * the same submission, so the server can recognise it; changing anything makes
 * it a new one.
 */
export function createSubmissionKey() {
  let id: string | undefined;
  let signature: string | undefined;

  return (parts: readonly unknown[]) => {
    const next = JSON.stringify(parts);

    if (next !== signature || !id) {
      signature = next;
      id = crypto.randomUUID();
    }

    return id;
  };
}
