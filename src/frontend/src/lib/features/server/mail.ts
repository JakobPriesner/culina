import { http, request, type AppError } from '$api';
import type { components } from '$api/generated/schema';

/**
 * The mail account Culina sends from: one form with one Save (host, port and password only make sense together),
 * and a test that goes through what was saved. The password is write-only: the box starts empty and empty keeps the
 * stored one, but only for the same server.
 */
export type MailSettings = components['schemas']['SettingsGetMailResponse'];

export type MailSecurity = 'none' | 'starttls' | 'tls';

export interface MailDraft {
  enabled: boolean;
  host: string;
  port: string;
  security: string;
  username: string;
  password: string;
  fromAddress: string;
  fromName: string;
}

/** The usual port for each kind of security, filled in when the kind changes and the port was the old kind's. */
export const usualPort: Record<MailSecurity, string> = { none: '25', starttls: '587', tls: '465' };

export const draftOf = (settings: MailSettings): MailDraft => ({
  enabled: settings.enabled,
  host: settings.host,
  port: String(settings.port),
  security: settings.security,
  username: settings.username,
  password: '',
  fromAddress: settings.fromAddress,
  fromName: settings.fromName
});

/** Whether the stored password would be dropped: the server changed and none was typed. */
export const needsPasswordAgain = (draft: MailDraft, settings: MailSettings): boolean =>
  settings.passwordConfigured && draft.password === '' && draft.host.trim() !== settings.host;

export function readMailSettings() {
  return request(() => http.GET('/api/v1/settings/mail'));
}

export function saveMailSettings(draft: MailDraft) {
  return request(() =>
    http.PUT('/api/v1/settings/mail', {
      body: {
        enabled: draft.enabled,
        host: draft.host,
        // Not a number is sent as one the server refuses, so the field is marked rather than silently zeroed here.
        port: /^\d+$/.test(draft.port.trim()) ? Number(draft.port.trim()) : 0,
        security: draft.security,
        username: draft.username,
        // Empty keeps the stored password: there is no way to read it back, so there is nothing to show.
        password: draft.password === '' ? null : draft.password,
        fromAddress: draft.fromAddress,
        fromName: draft.fromName
      }
    })
  );
}

/** Sends a test mail to the administrator through the saved account; the failure says what the server answered. */
export async function sendTestMail(): Promise<AppError | null> {
  const result = await request(() => http.POST('/api/v1/settings/mail/tests'));

  return result.ok ? null : result.error;
}
