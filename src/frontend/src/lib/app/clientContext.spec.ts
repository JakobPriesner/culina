import { afterEach, describe, expect, it, vi } from 'vitest';

import { describeClient, describeMoment, learnClientHints } from './clientContext';

/*
 * This runs while something is already going wrong, on browsers that offer
 * different halves of what it reads. What has to hold is that it takes what is
 * there and never makes things worse.
 */

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('describeClient', () => {
  it('names the browser by its real brands, with what it only tells when asked', async () => {
    stubNavigator({
      userAgentData: {
        brands: [{ brand: 'Not)A;Brand', version: '99' }],
        mobile: true,
        platform: 'Android',
        getHighEntropyValues: () =>
          Promise.resolve({
            architecture: 'arm',
            model: 'Pixel 9',
            platformVersion: '15.0.0',
            fullVersionList: [
              { brand: 'Chromium', version: '140.0.7339.0' },
              { brand: 'Not_A Brand', version: '24.0.0.0' }
            ]
          })
      }
    });

    learnClientHints();
    await Promise.resolve();
    await Promise.resolve();

    expect(describeClient()).toEqual(
      expect.objectContaining({
        brands: ['Chromium 140.0.7339.0'],
        platform: 'Android',
        platformVersion: '15.0.0',
        architecture: 'arm',
        model: 'Pixel 9',
        mobile: true
      })
    );
  });

  it('leaves out whatever the browser refuses, rather than failing the report', () => {
    vi.stubGlobal('matchMedia', () => {
      throw new Error('not here');
    });

    const client = describeClient();

    expect(client.colorScheme).toBeUndefined();
    expect(client.sessionId).toEqual(expect.any(String));
  });
});

describe('describeMoment', () => {
  it('says when, by the device clock, and whether the page was online', () => {
    const moment = describeMoment();

    expect(Date.parse(moment.occurredAt!)).not.toBeNaN();
    expect(moment.online).toBe(navigator.onLine);
  });
});

function stubNavigator(extra: object) {
  vi.stubGlobal('navigator', Object.assign(Object.create(navigator) as Navigator, extra));
}
