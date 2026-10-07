import { request, type APIRequestContext, type APIResponse } from '@playwright/test';

/** Tells the instance to accept new accounts once for the whole run (it refuses until an administrator says so); done here because sign-in is rate limited per account and six workers would hit the same limit. */
const origin = 'http://localhost:4173';

/** Signs in, waiting out the per-account rate limit (two runs a minute apart will meet it) instead of failing. */
async function signInOnce(
  context: APIRequestContext,
  who: { email: string; password: string }
): Promise<APIResponse> {
  const attempt = () =>
    context.post('/api/v1/sessions', { headers: { Origin: origin }, data: who });

  const first = await attempt();

  if (first.status() !== 429) {
    return first;
  }

  const seconds = Number(first.headers()['retry-after'] ?? '60');
  const wait = Number.isFinite(seconds) ? Math.min(Math.max(seconds, 1), 90) : 60;

  console.log(`Signing in is rate limited; waiting ${wait}s before trying once more.`);
  await new Promise((resolve) => setTimeout(resolve, wait * 1000));

  return attempt();
}

export default async function openRegistration(): Promise<void> {
  const email = process.env['CULINA_E2E_EMAIL'];
  const password = process.env['CULINA_E2E_PASSWORD'];

  if (!email || !password) {
    // No backend: every signed-in suite skips.
    return;
  }

  const context = await request.newContext({ baseURL: origin });

  try {
    const signedIn = await signInOnce(context, { email, password });

    if (!signedIn.ok()) {
      throw new Error(`Could not sign in as ${email}: ${await signedIn.text()}`);
    }

    const csrf = (await context.storageState()).cookies.find(
      (cookie) => cookie.name === 'culina.csrf'
    )?.value;

    const opened = await context.put('/api/v1/settings/registration', {
      headers: { Origin: origin, 'X-Culina-CSRF': csrf ?? '' },
      data: { openRegistration: true, requireInvitation: false, maxUsers: 1000 }
    });

    if (!opened.ok()) {
      throw new Error(
        `CULINA_E2E_* must be an administrator — this suite opens registration: ${await opened.text()}`
      );
    }
  } finally {
    await context.dispose();
  }
}
