import type { CapacitorConfig } from '@capacitor/cli';

/**
 * The installed apps (docs/native-app-design.md, part 5): the website's code built by `pnpm build:native`, wrapped in an
 * Xcode and a Gradle project under native/. `pnpm native:sync` copies the bundle into both.
 */
const config: CapacitorConfig = {
  appId: 'app.culina',
  appName: 'Culina',
  webDir: 'build-native',
  ios: { path: 'native/ios' },
  android: { path: 'native/android' },
  server: {
    // The apps' origins, `capacitor://app.culina.localhost` and `https://app.culina.localhost`: the server's CORS
    // allow-list names exactly these (NativeAppCorsMiddleware). Not plain `localhost`, which any local dev server shares.
    hostname: 'app.culina.localhost',
    androidScheme: 'https'
  },
  plugins: {
    // Off: the app's API calls stream (AI drafts, import progress), and the native HTTP layer buffers whole bodies.
    CapacitorHttp: { enabled: false },
    CapacitorCookies: { enabled: false }
  }
};

export default config;
