# Culina's iOS and Android apps

The website's own code, built into an app bundle (`pnpm build:native`) and
wrapped in the two Capacitor 8 projects here. The design, and why it looks the
way it does, is in [`docs/native-app-design.md`](../../../docs/native-app-design.md).

## Prerequisites

- **iOS:** Xcode with its simulator components. On a Mac where `xcrun simctl`
  fails, run `sudo xcodebuild -runFirstLaunch` once. Without CoreSimulator,
  `xcodebuild` cannot even build for a device. Swift packages come through SPM;
  there is no CocoaPods step.
- **Android:** Android Studio (or the command-line tools), JDK 21 and an
  Android SDK. Gradle comes from the wrapper.

## Working on the apps

```bash
make native-ios       # build the bundle, copy it in, open Xcode
make native-android   # the same for Android Studio
make native-sync      # only build and copy (after a web change)
```

The app asks for its server on first launch. For development, point it at an
API the phone can reach. Run the API on the LAN, e.g.
`ASPNETCORE_URLS=http://0.0.0.0:5050`, because macOS AirPlay holds port 5000.
Then enter `http://<this Mac's LAN address>:5050`. The API answers the app's
fixed origins with CORS (`NativeAppCorsMiddleware`). A Vite dev server address
does not work here: its `/api` proxy adds no CORS headers.

## What is tracked

Both projects are source, edited in Xcode and Android Studio, and committed.
Their own `.gitignore`s keep out what `cap sync` copies in (`App/App/public`,
`app/src/main/assets/public`, the generated `capacitor.config.json`) and build
output. Signing material (`*.jks`, `*.keystore`, `*.p8`, provisioning profiles)
never enters the repository.

## Before calling a build good

Run the device checklists in `docs/native-app-design.md` (2.6 for the shell, 4.6
for timers) on a real iPhone and a real Android phone. A simulator cannot show
the keyboard, WKWebView, lock screen or battery behaviour that matters here.
