import { request, type APIRequestContext, type APIResponse } from '@playwright/test';

/**
 * Tells the instance to accept new accounts, once for the whole run.
 *
 * Every flow owns an account, and the first run against a fresh instance has to
 * create them — which the instance refuses until an administrator says so. Done
 * here rather than where the accounts are made, because signing in is rate
 * limited per account: six workers each signing in as the administrator to ask
 * the same question is six workers hitting the same limit.
 */
const origin = 'http://localhost:4173';

/**
 * Signs in, waiting out a rate limit rather than failing the run for it.
 *
 * Signing in is limited per account, which is exactly right, and two runs of
 * this suite a minute apart will meet it. That is the limit working, not a
 * fault — so this waits the time the server asks for and tries once more.
 */
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
    // No backend, and every signed-in suite skips. Nothing to arrange.
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
