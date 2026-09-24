import { describe, expect, it } from 'vitest';

import {
  databaseVariable,
  limitVariable,
  toDatabaseRequest,
  toServerDraft,
  toServerRequest,
  unreadableNumbers
} from './types';

const read = {
  cookies: { secure: true, sessionDays: 30, renewAfterHours: 24 },
  forwardedHeaders: { knownProxies: ['10.0.0.2', '10.0.0.3'], knownNetworks: [] },
  rateLimits: {
    loginPerIpPerMinute: 10,
    loginPerAccountPerMinute: 5,
    registerPerIpPerHour: 5,
    invitationPerIpPerHour: 10,
    importsPerHour: 30,
    sourceRequestsPerHour: 1500,
    sharedRecipesPerIpPerMinute: 120,
    assistantRequestsPerHour: 60,
    requestsPerSessionPerMinute: 600
  },
  telemetry: { otlpEndpoint: null, otlpProtocol: 'grpc' },
  connection: { remoteAddress: '127.0.0.1', forwarded: false, proxyTrusted: false },
  pinned: [],
  writable: true
};

describe('server settings, as a form holds them', () => {
  it('sends back exactly what it read when nothing was edited', () => {
    // An unedited form must be "no change" on the server, or saving it would
    // restart the instance for nothing.
    const request = toServerRequest(toServerDraft(read));

    expect(request.cookies).toEqual(read.cookies);
    expect(request.forwardedHeaders).toEqual(read.forwardedHeaders);
    expect(request.rateLimits).toEqual(read.rateLimits);
    expect(request.telemetry).toEqual({ otlpEndpoint: null, otlpProtocol: 'grpc' });
  });

  it('reads a typed list the way the server does: trimmed, blanks dropped', () => {
    const draft = { ...toServerDraft(read), knownProxies: ' 10.0.0.2 ,, 10.0.0.9, ' };

    expect(toServerRequest(draft).forwardedHeaders.knownProxies).toEqual(['10.0.0.2', '10.0.0.9']);
  });

  it('names every box that is not a whole number, before anything is sent', () => {
    const draft = toServerDraft(read);
    draft.sessionDays = '30 days';
    draft.limits.importsPerHour = '';

    expect(unreadableNumbers(draft)).toEqual(['sessionDays', 'importsPerHour']);
  });

  it('keeps the database password when the box is left empty', () => {
    // The form never had the password to send back.
    const request = toDatabaseRequest({
      host: ' db ',
      port: '5432',
      name: 'culina',
      username: 'culina_app',
      password: '',
      requireSsl: false,
      maxPoolSize: '20'
    });

    expect(request.password).toBeNull();
    expect(request.host).toBe('db');
  });

  it('names a setting by the variable that pins it', () => {
    expect(limitVariable('loginPerIpPerMinute')).toBe('RateLimits__LoginPerIpPerMinute');
    expect(databaseVariable('requireSsl')).toBe('Database__RequireSsl');
  });
});
