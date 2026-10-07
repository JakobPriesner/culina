import type { components } from '$api/generated/schema';

/**
 * What a report says about the browser and device, never who (no address, referrer or typed text).
 * Every reading is optional and none may throw: this runs while something is already going wrong.
 */
export type ClientContext = components['schemas']['LogRecordsCreateClient'];

/** What changes from one record to the next: when, and what state the page was in. */
export type MomentContext = Pick<
  components['schemas']['LogRecordsCreateRecord'],
  'occurredAt' | 'pageAge' | 'online' | 'visible' | 'heapUsed' | 'heapLimit'
>;

/** Not in the DOM typings yet: Chromium's client hints, network and memory readings. */
interface BrowserNavigator extends Navigator {
  readonly userAgentData?: {
    readonly brands: readonly Brand[];
    readonly mobile: boolean;
    readonly platform: string;
    getHighEntropyValues(hints: string[]): Promise<{
      readonly architecture?: string;
      readonly model?: string;
      readonly platformVersion?: string;
      readonly fullVersionList?: readonly Brand[];
    }>;
  };
  readonly connection?: {
    readonly effectiveType?: string;
    readonly downlink?: number;
    readonly rtt?: number;
    readonly saveData?: boolean;
  };
  readonly deviceMemory?: number;
  /** Safari's own word for an app opened from the home screen. */
  readonly standalone?: boolean;
}

interface Brand {
  readonly brand: string;
  readonly version: string;
}

interface BrowserPerformance extends Performance {
  readonly memory?: { readonly usedJSHeapSize: number; readonly jsHeapSizeLimit: number };
}

/** One per page load, so the records of one page can be told from another's. */
const sessionId = randomId();

/** What the browser only tells after being asked, and then only asynchronously. */
let highEntropy: Pick<ClientContext, 'architecture' | 'model' | 'platformVersion' | 'brands'> = {};

/** Asks once for the promise-gated details so they are ready when something goes wrong; refusals are left out. */
export function learnClientHints(): void {
  read(() =>
    browser()
      .userAgentData?.getHighEntropyValues([
        'architecture',
        'model',
        'platformVersion',
        'fullVersionList'
      ])
      .then((values) => {
        highEntropy = {
          architecture: text(values.architecture),
          model: text(values.model),
          platformVersion: text(values.platformVersion),
          brands: values.fullVersionList ? brandsOf(values.fullVersionList) : undefined
        };
      })
      .catch(() => undefined)
  );
}

export function describeClient(): ClientContext {
  const navigator = browser();
  const hints = navigator.userAgentData;
  const connection = navigator.connection;

  return {
    sessionId,
    locale: read(() => text(document.documentElement.lang)),
    languages: read(() => navigator.languages.slice(0, 10)),
    timeZone: read(() => Intl.DateTimeFormat().resolvedOptions().timeZone),
    brands: read(() => (hints ? brandsOf(hints.brands) : undefined)),
    platform: read(() => text(hints?.platform)),
    mobile: hints?.mobile,
    ...highEntropy,
    screenWidth: read(() => screen.width),
    screenHeight: read(() => screen.height),
    viewportWidth: read(() => window.innerWidth),
    viewportHeight: read(() => window.innerHeight),
    pixelRatio: read(() => window.devicePixelRatio),
    orientation: read(() => screen.orientation?.type),
    colorScheme: read(() => (matches('(prefers-color-scheme: dark)') ? 'dark' : 'light')),
    reducedMotion: read(() => matches('(prefers-reduced-motion: reduce)')),
    displayMode: read(displayMode),
    cores: read(() => navigator.hardwareConcurrency),
    deviceMemory: read(() => navigator.deviceMemory),
    touchPoints: read(() => navigator.maxTouchPoints),
    connection: connection?.effectiveType,
    downlink: connection?.downlink,
    roundTrip: connection?.rtt,
    saveData: connection?.saveData,
    navigationType: read(navigationType),
    serviceWorker: read(() => Boolean(navigator.serviceWorker?.controller))
  };
}

export function describeMoment(): MomentContext {
  const memory = read(() => (performance as BrowserPerformance).memory);

  return {
    occurredAt: new Date().toISOString(),
    pageAge: read(() => Math.round(performance.now())),
    online: read(() => navigator.onLine),
    visible: read(() => document.visibilityState === 'visible'),
    heapUsed: memory?.usedJSHeapSize,
    heapLimit: memory?.jsHeapSizeLimit
  };
}

function browser(): BrowserNavigator {
  return navigator as BrowserNavigator;
}

/** `Chromium 140`, without the made-up brands Chromium mixes in. */
function brandsOf(brands: readonly Brand[]): string[] {
  return brands
    .filter(({ brand }) => !/not.?a.?brand/i.test(brand))
    .slice(0, 10)
    .map(({ brand, version }) => `${brand} ${version}`);
}

function displayMode(): string {
  const modes = ['fullscreen', 'standalone', 'minimal-ui'];
  const mode = modes.find((candidate) => matches(`(display-mode: ${candidate})`));

  return mode ?? (browser().standalone ? 'standalone' : 'browser');
}

function navigationType(): string | undefined {
  const [entry] = performance.getEntriesByType('navigation') as PerformanceNavigationTiming[];

  return entry?.type;
}

function matches(query: string): boolean {
  return window.matchMedia?.(query).matches ?? false;
}

function randomId(): string | undefined {
  return read(() => crypto.randomUUID());
}

/** A reading a browser does not support, or refuses, is left out rather than failing the report. */
function read<T>(reading: () => T): T | undefined {
  try {
    return reading() ?? undefined;
  } catch {
    return undefined;
  }
}

/** An empty string says nothing, so it is not sent. */
function text(value: string | undefined): string | undefined {
  return value || undefined;
}
