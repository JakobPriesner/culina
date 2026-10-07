/** An id stable while the submitted parts are unchanged, so the server recognises a retry. */
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
