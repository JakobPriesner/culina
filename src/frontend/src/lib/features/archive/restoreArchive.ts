import { http, request } from '$api';

/** Sends an archive file back in; the server reports how many recipes it wrote and how many it had to skip. */
export function restoreArchive(householdId: string, file: File) {
  const body = new FormData();

  body.append('file', file);

  return request(() =>
    http.POST('/api/v1/households/{householdId}/archive', {
      params: { path: { householdId } },
      body: body as unknown as { file: string },
      // FormData sets its own multipart boundary; JSON-serialising it would send "[object FormData]".
      bodySerializer: (value: unknown) => value as FormData
    })
  );
}
