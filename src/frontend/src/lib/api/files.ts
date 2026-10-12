import { authorization, credentialsMode } from './credentials';
import { apiUrl } from './instance';
import { send } from './transport';

/**
 * Reads a file the API serves (a photo, the archive) as a blob, signed in the way this build signs in. The website lets
 * `<img>` and `<a download>` fetch these themselves with the cookie; the app cannot attach its bearer token to either,
 * so it reads them here. Null when the file could not be read.
 */
export async function fetchFile(path: string, signal?: AbortSignal): Promise<Blob | null> {
  try {
    const response = await send(
      new Request(apiUrl(path), {
        credentials: credentialsMode,
        headers: authorization(false),
        signal
      })
    );

    return response.ok ? await response.blob() : null;
  } catch {
    return null;
  }
}
