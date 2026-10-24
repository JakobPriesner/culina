import { http, ok, request, type Result } from '$api';

/** An app asking, through OAuth, to connect an AI agent to the signed-in person's account. Every string is the app's own word: text, never markup. */
export interface AgentAuthorization {
  readonly id: string;
  readonly clientName: string;
  /** The host whose metadata document vouches for the name; null when the app registered itself and nothing does. */
  readonly vouchedForBy: string | null;
  /** Where the browser is sent with the code, which the person must see. */
  readonly redirectHost: string;
  /** A loopback address: any program on this computer could be listening there. */
  readonly redirectsToThisComputer: boolean;
}

/** The pending request, for the consent screen. */
export async function readAgentAuthorization(id: string): Promise<Result<AgentAuthorization>> {
  const result = await request(() =>
    http.GET('/api/v1/agent-authorizations/{authorizationId}', {
      params: { path: { authorizationId: id } }
    })
  );

  return result.ok
    ? ok({
        id: result.value.authorizationId,
        clientName: result.value.clientName,
        vouchedForBy: result.value.verifiedHost ?? null,
        redirectHost: result.value.redirectHost,
        redirectsToThisComputer: result.value.redirectsToThisComputer
      })
    : result;
}

/** Answers the request; the value is the app's return address, carrying the code or the refusal, to navigate to. */
export async function decideAgentAuthorization(
  id: string,
  approved: boolean
): Promise<Result<string>> {
  const result = await request(() =>
    http.PUT('/api/v1/agent-authorizations/{authorizationId}/decision', {
      params: { path: { authorizationId: id } },
      body: { approved }
    })
  );

  return result.ok ? ok(result.value.redirectUri) : result;
}
