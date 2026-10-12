# Culina — native app beside the website

The plan for compiling Culina into native iOS, Android and (perhaps) desktop
apps from the same Svelte 5 / TypeScript code that serves the website. Tracked
as `culina-v2-tj23`; each numbered part below is one planning spike. This is a
planning document: it records what the code assumes today, what each option
costs, and what the first prototypes must prove. Nothing here is installed yet.

Builds on [`motion-and-native-design.md`](motion-and-native-design.md) (the
first Capacitor notes: safe areas, keyboard, lifecycle) and
[`kitchen-pwa-design.md`](kitchen-pwa-design.md) (timers, notifications, wake
lock, media session as they work on the web).

---

## Contents

1. [What the web app assumes today](#1-what-the-web-app-assumes-today)
2. [Packaging runtime: Capacitor or Tauri v2](#2-packaging-runtime-capacitor-or-tauri-v2) — `tj23.1`
3. [Connecting to an instance: network, auth, streaming](#3-connecting-to-an-instance-network-auth-streaming) — `tj23.2`
4. [Kitchen timers on the lock screen](#4-kitchen-timers-on-the-lock-screen) — `tj23.3`
5. [Build pipeline: web and native from one tree](#5-build-pipeline-web-and-native-from-one-tree) — `tj23.4`
6. [First steps](#6-first-steps)
7. [Push relay](#7-push-relay) — `tj23.10`

---

## 1. What the web app assumes today

Culina is one origin. The .NET host serves the static SPA and `/api` together,
and the following code depends on that. A native shell serves the UI from its
own origin (`capacitor://localhost`, `https://localhost`, `tauri://localhost`),
so every item below either breaks or needs a second path.

| Assumption | Where | Consequence in a native shell |
| --- | --- | --- |
| API calls use relative `/api/v1/...` paths with no base URL | `lib/api/client.ts:38-42`, `events.ts`, `ask.ts` | Requests go to the shell's own origin and 404 |
| Image URLs are hand-built `/api/...` paths | `features/recipes/recipeImage.ts`, `import/IntakeComparison.svelte:29`, archive download link | Every `<img>` and the archive download break the same way |
| Session cookie is `__Host-culina.session`, HttpOnly, `SameSite=Lax` | `Api/Authentication/SessionCookies.cs:83-101` | A cross-site cookie: Lax withholds it on `fetch`, and WebKit blocks third-party cookies outright |
| CSRF token is read from the `culina.csrf` cookie via `document.cookie` | `lib/api/cookies.ts` | The shell's `document.cookie` cannot see the API origin's cookies |
| Unsafe requests must carry `Origin == scheme://host` of the API | `Api/Middleware/SameOriginMiddleware.cs` | `capacitor://localhost` is refused; there is no allow-list |
| No CORS at all; CORP and COOP `same-origin` | `Api/Middleware/SecurityHeaders.cs` | Cross-origin reads are blocked by the webview |
| Document CSP is `connect-src 'self'; img-src 'self'` with a per-response nonce substituted into `index.html` | `Api/Infrastructure/AppShell.cs` | The packaged `index.html` keeps the literal `__CULINA_NONCE__` and gets no CSP unless the shell adds one |
| AI drafting and import progress are SSE read from a streamed `fetch` body | `lib/api/eventStream.ts`, six `ServerSentEvents` endpoints | Whatever carries `fetch` in the shell must stream |
| Service worker owns offline reads, share target, push and notification actions | `src/service-worker*` | Not available on custom schemes in WKWebView (see 2.2) |
| Share and invite links use `location.origin` | `ShareRecipeSheet.svelte:32`, `InvitePanel.svelte:25` | Would produce `capacitor://localhost/shared/...` |
| Push is Web Push with VAPID; endpoint allow-list is FCM/Mozilla/Apple web push | `Infrastructure/Import/IntakeNotifications.cs:113-116` | A native app gets APNs/FCM device tokens instead |

The browser APIs the kitchen depends on, all feature-detected today: Screen
Wake Lock (`cooking/wakeLock.svelte.ts`), notifications with actions
(`cooking/timerNotification.ts`), Badging (`app/badgeManager.svelte.ts`), Media
Session with a silent audio loop (`cooking/mediaSession.svelte.ts`), Web Audio
chime (`cooking/kitchenAudio.ts`), `navigator.vibrate` (`app/haptics.ts`),
Document Picture-in-Picture (`cooking/pip.svelte.ts`), Web Share and Web Share
Target, clipboard read/write, file inputs for photos (no `capture`, no
`getUserMedia`), IndexedDB (`culina-kitchen`, `culina-recipe-intake`).

Timers are absolute deadlines (`endsAt`, epoch ms) per cooking session and step
index, persisted in IndexedDB and mirrored to localStorage
(`cooking/timerState.ts`, `timers.svelte.ts`). Alarms fire only from the page's
one-second clock; the service worker only applies notification actions. This
is the weakness native timers are meant to remove (part 4).

### Probe: the built shell without its host

On 12 October 2026 a copy of `src/frontend/build` was served from a plain
static server on a separate origin with no Culina API, which is what a packaged
shell is before it knows an instance:

- The shell boots. Absolute `/_app/...` paths resolve against the shell's own
  root, the route-preload table runs, and the literal `nonce="__CULINA_NONCE__"`
  is harmless when no CSP is sent. `paths: { relative: false }` is therefore
  not an obstacle for either runtime, since both serve the bundle from the root
  of their own origin.
- `GET /api/v1/users/me`, `/users/me/settings` and `/setup` hit the shell's
  origin and 404. The app lands on "Culina antwortet nicht — Du bist weiterhin
  angemeldet …". That copy is wrong for a native app that has never connected:
  it needs a "connect to your kitchen" state instead (part 3).
- The service worker registered (Chromium on `http://localhost`). In a native
  shell it should not be registered at all (2.2).

No device or simulator prototype has been run yet. This Mac has Xcode 27, but
its CoreSimulator components are not installed (`xcrun simctl` fails). They
come with `sudo xcodebuild -runFirstLaunch`. Android Studio is not installed.

---

## 2. Packaging runtime: Capacitor or Tauri v2

### 2.1 The candidates as of October 2026

**Capacitor 8** (8.0 December 2025; 8.5 July 2026 adopted UIScene for the
iOS 27 SDK). A native iOS/Android project that hosts a system webview
(WKWebView, Android System WebView) and serves the built web assets from a
local scheme. Plugins are TypeScript APIs over Swift/Kotlin. New iOS projects
use Swift Package Manager. There is no first-party desktop target; the
community Electron platform is the only route.

**Tauri 2** (stable since October 2024; core plugin crate 2.6 April 2026). A
Rust core that hosts the system webview: WKWebView on iOS and macOS, Android
System WebView, WebView2 on Windows, WebKitGTK on Linux. Desktop is the mature
half (bundles, signing, updater, tray). Mobile works, but plugins need a Rust
crate plus Swift/Kotlin, and the official mobile set is smaller.

Both run Culina's existing static output unchanged: Svelte 5 runes are
compiled away, `adapter-static` already produces a plain `index.html` with
hashed assets, and part 1's probe shows the shell does not need the .NET host to
boot. Neither runtime is a reason to change the frontend framework, the router
or the build.

### 2.2 What changes inside any webview shell

These hold for both runtimes, so they are not a point of comparison:

- **No service worker.** WKWebView exposes `navigator.serviceWorker` only for
  `http(s)` origins listed as App-Bound Domains, never for `capacitor://` or
  `tauri://`. Android Capacitor (`https://localhost`) can register one, but
  making the two platforms behave differently is not worth it. The native
  build skips registration (`lib/app/updates.svelte.ts`). Each worker job then
  needs a native or in-page replacement:
  - Offline API reads (`culina-private` cache): move them to an in-page
    IndexedDB cache inside the API client. The assets are local already.
  - Update prompt mid-recipe: not needed, because the bundle changes only with
    an app update.
  - Notification actions (+1/+2 min, dismiss, next step): native local
    notifications (part 4).
  - Share target: a native share extension (`iphone-pwa-share-target` memory).
  - Web Push: APNs/FCM.
- **No Document Picture-in-Picture.** It is Chromium-only. The floating
  cooking window stays a desktop-browser feature; on phones, Live Activities
  and ongoing notifications take its place.
- **`navigator.vibrate` does nothing on iOS.** Haptics need a native call.
- **Web Push becomes native push.** The backend has to send to APNs/FCM device
  tokens as well as VAPID subscriptions (part 3).
- **CSP moves into the bundle.** The shell has no per-response nonce, so the
  native build replaces the nonce placeholder and states its own policy: a
  `<meta>` CSP for Capacitor, or `app.security.csp` for Tauri, which adds hashes
  itself. Its `connect-src` and `img-src` must name the configured instance,
  which is known only at run time (part 3).

### 2.3 Comparison

| | Capacitor 8 | Tauri 2 |
| --- | --- | --- |
| Languages added | Swift/Kotlin only where Culina writes a plugin | Rust, always; plus Swift/Kotlin for mobile plugins |
| Toolchain | Node, Xcode, Android Studio/Gradle | Node, rustup + iOS/Android targets, Xcode, Android Studio + NDK |
| Native project | `ios/`, `android/`: ordinary Xcode/Gradle projects, checked in and edited directly | `src-tauri/` (Rust) + generated `gen/apple`, `gen/android` |
| Mobile maturity | The product's whole focus for 6+ years; around a million weekly downloads | Mobile since 2.0; smaller plugin set and community |
| Desktop | None first-party (community Electron) | First-class: macOS, Windows, Linux bundles, updater |
| Webview origin | `capacitor://localhost` (iOS), `https://localhost` (Android) | `tauri://localhost` (Apple, Linux), `http(s)://tauri.localhost` (Windows, Android) |
| HTTP outside the webview | CapacitorHttp patches `fetch`/XHR; bodies are buffered, so SSE does not stream | `plugin-http` (Rust reqwest) exposes a `fetch` |
| App size overhead | Small (system webview) | Small (system webview + Rust binary) |

Kitchen-critical features. "Web" means the code Culina already has.

| Need | Web today | Capacitor | Tauri 2 (mobile) |
| --- | --- | --- | --- |
| Screen stays on while cooking | Wake Lock API; WKWebView support must be measured on a device | `@capacitor-community/keep-awake` | Community plugins only, or the web API |
| Timer alarm with the app suspended or killed | Not possible | `@capacitor/local-notifications`: OS schedules at an absolute time, with action buttons | Official notification plugin: actions are mobile-only; scheduling exists but is thinly documented |
| Lock screen / Dynamic Island countdown | Not possible | Custom plugin + Swift widget extension (community plugins exist) | Custom plugin (Rust + Swift) + the same widget extension |
| Android ongoing countdown notification | Not possible | Local notification (chronometer) or a small Kotlin plugin | Small Kotlin plugin |
| Photo capture | `<input type=file accept=image/*>` (opens camera or library on both OSes) | Same input, or `@capacitor/camera` | Same input |
| Haptics | Android only (`vibrate`) | `@capacitor/haptics` | Official `haptics` plugin |
| Push | Web Push (VAPID) | `@capacitor/push-notifications` (APNs/FCM) | No official plugin |
| Incoming share sheet | Android PWA only | Custom share extension target | Custom share extension target |
| Deep links | Routes | `@capacitor/app` `appUrlOpen` | Official `deep-link` plugin |
| App badge | Badging API (installed PWA) | Community badge plugin | None official |

The pattern: everything Culina already does on the web keeps working in both
shells. Everything the native app exists for is either an off-the-shelf
Capacitor plugin or, for Live Activities and ongoing notifications, Swift/Kotlin
that Culina writes anyway. In Tauri, the same Swift/Kotlin needs Rust glue on
top.

### 2.4 Desktop versus mobile

The epic's motivation is mobile kitchen capability. Desktop gains little from
a native shell. In Chrome and Edge, the installed PWA already has the service
worker, notifications with actions, badges, Wake Lock and Document PiP (the
floating timer window), and Safari can add it to the Dock. A desktop app
would add a tray icon, auto-start and an updater, none of which a recipe app
needs. iOS is where the web platform falls shortest: no alarm after
suspension, no Live Activity, no share-sheet target, and push only for
home-screen apps. Android is second, mainly for the ongoing timer notification.

**Priority:** iOS first, Android second, and desktop stays the installed PWA.

### 2.5 Recommendation and what would change it

Recommendation: **Capacitor for iOS and Android; no native desktop target.**

- It keeps the codebase TypeScript + C#. Rust would be a third language
  carried for one packaging layer.
- Every kitchen feature in 2.3 is an existing Capacitor plugin or native code
  Culina would write either way. Live Activities need a Swift widget extension
  in both runtimes, and Capacitor's checked-in Xcode project is the natural
  home for it.
- The 6 October motion review already assumed Capacitor (keyboard, app
  lifecycle, safe areas), so those notes carry over unchanged.
- The cost to watch: CapacitorHttp cannot stream. Culina's AI drafting and
  import progress stream, so `fetch` cannot simply be patched globally (part 3
  decides the network path).

Choose Tauri instead if a native desktop app becomes a goal in its own right
(offline-first desktop, OS integration the PWA cannot reach), or if Capacitor's
networking proves unworkable for streaming and a Rust HTTP layer is wanted. The
frontend work is identical, since the shell is thin. That keeps this a cheap
decision to revisit: everything in parts 3–5 is runtime-neutral except the
plugin names.

### 2.6 What the first prototype must prove

Run on a real iPhone and a mid-range Android phone, not only simulators
(motion review: keyboard and WKWebView behaviour cannot be emulated):

1. The bundle boots from the local scheme, client routes survive a cold start
   on a deep path, and Back on Android closes an open sheet before navigating.
2. `navigator.wakeLock.request('screen')` in WKWebView holds the screen on
   through a 10-minute idle, and comes back after backgrounding. If it does
   not, use the keep-awake plugin.
3. Media Session metadata and the silent-audio loop surface lock-screen
   controls from WKWebView, or are dropped in favour of the Live Activity.
4. Photo file inputs offer camera and library, and HEIC arrives intact.
5. The streamed `fetch` used by `eventStream.ts` delivers SSE incrementally to
   a configured instance (part 3).
6. Cold-start time and bundle size compared with the PWA on the same phone.

**Status (13 October 2026, `tj23.7`).** The shell exists:
`src/frontend/capacitor.config.ts`, plus `native/ios` and `native/android`
(Capacitor 8.5, SPM), on the hostname `app.culina.localhost`. Native HTTP and
cookie patching are off. `lib/app/nativeShell.ts` registers:
- the Keychain/Keystore token store (`@aparajita/capacitor-secure-storage`, no
  iCloud sync);
- the photo source;
- `@capacitor-community/keep-awake` in place of the web view's Wake Lock, so
  item 2 no longer depends on WKWebView;
- Taptic/vibration haptics (`@capacitor/haptics`), since iOS has no
  `navigator.vibrate`;
- Android's Back: close the top popover or dialog, else go back, else leave.

A website build replaces these modules with stubs (`build-tools/nativeOnly.ts`),
and `verify:release` fails if Capacitor's plugin names appear in `build/`.
iOS allows plain http only on the local network (`NSAllowsLocalNetworking`).
Android permits cleartext, and the app itself refuses http to public hosts.

None of this has been compiled or run yet. The planning Mac's Xcode cannot load
its simulator plug-in, so `xcodebuild` fails before building, and it has no
Android SDK. The checklist above is open as its own bead.

---

## 3. Connecting to an instance: network, auth, streaming

Culina is self-hosted, so a store-installed app cannot know its server at build
time. Every instance has its own origin, and some are reachable only on a home
network. The app learns the origin on first launch and talks to it from a
different origin for the rest of its life.

### 3.1 Where the UI comes from

Two shapes are possible, and the choice settles most of what follows.

**A. Bundled UI** (the assumption in part 2). The app ships the built SPA and
calls the instance cross-origin.
- Works offline from a cold start: the shopping list in the supermarket, a
  recipe in a kitchen with bad Wi-Fi.
- Native code can live in the bundle behind a build flag.
- Clearly within App Store rules.
- Costs: the API must accept a cross-origin client (3.3), and one app version
  must talk to servers of many versions (3.6).

**B. Remote UI.** The app ships only a "connect" screen and then navigates its
webview to `https://kitchen.example`. The Home Assistant companion app works
this way, with a native bridge into a server-hosted frontend
([external authentication](https://developers.home-assistant.io/docs/frontend/external-authentication/)).
- Same-origin again: cookies, CSRF, `SameOriginMiddleware` and the CSP all
  work unchanged, and the UI always matches its server.
- Costs:
  - No offline cold start on iOS. WKWebView allows service workers only for
    App-Bound Domains, which are a fixed list of at most ten in `Info.plist`
    and so cannot include a self-hoster's domain.
  - Every page the instance serves gets the native bridge.
  - Version skew moves to the bridge: a newer server UI calling plugin methods
    an older app lacks.
  - Capacitor does not officially support choosing `server.url` at run time.

**Recommendation: A, bundled UI.** Offline cooking and shopping are core to
Culina (`kitchen-pwa-design.md`), and A is the only shape that keeps them on
iOS. Everything else in this part assumes A.

### 3.2 Instance connection flow

First launch, before any other route renders:

1. **Connect.** One field: "Your Culina address". Accept `kitchen.example.com`
   and normalise it to `https://kitchen.example.com` (origin only, path
   dropped). Plain `http` is refused, except as an explicit opt-in for
   private-network addresses (`192.168.*`, `*.local`). iOS needs
   `NSAllowsLocalNetworking` for those and shows the local-network permission
   prompt.
2. **Verify.** `GET {origin}/api/v1/setup` (already anonymous) proves that a
   Culina instance answers, and its `stage` says whether it can be signed into.
   `account` and `database` show "finish setting up this server in a browser
   first"; the native app does not run setup. Its response carries
   `apiVersion` (the contract generation, `Contracts.ApiContract.Version`) for
   the compatibility check in 3.6.
3. **Sign in.** The existing sign-in form with native session delivery (3.4).
   Registration and invitations (`/join/{code}`) reuse their pages. An invite
   link opened on the phone carries its instance origin, so it can pre-fill
   step 1.

**Storage.** The instance origin and name go in Capacitor Preferences
(`UserDefaults` / `SharedPreferences`), which are not secret. The session token
goes in the Keychain / Android Keystore through a secure-storage plugin, keyed
by origin. localStorage is never used for the token.

**Switching.** Settings gains "Server" (native builds only). It shows the
origin, and "Use a different server" signs out (`DELETE` current session), then
clears every per-instance cache: ETag LRU, `culina-kitchen` IndexedDB, the
offline read cache, timers and badges. That is the same sweep `culina:forget`
does today. One active instance at a time: households already give one person
several kitchens inside one server, so remembering several servers is not
worth it.

**Before connecting**, the shell must not call `/api` at all. Part 1's probe
showed today's build treats a 404 from its own origin as "Culina antwortet
nicht — Du bist weiterhin angemeldet", which is false for an app that has never
connected.

### 3.3 Cross-origin options

| Option | How | Verdict |
| --- | --- | --- |
| Cross-site cookies + CORS with credentials | `SameSite=None`, `Access-Control-Allow-Credentials` | **No.** WebKit blocks third-party cookies in WKWebView unless the API domain is an App-Bound Domain, which a self-hosted origin cannot be |
| Native HTTP (CapacitorHttp patching `fetch`) | Requests leave through `URLSession`/OkHttp; cookies live in the native jar; no CORS | **No as the main path.** Bodies are buffered, so the six SSE endpoints stop streaming. Multipart uploads (photos) go through a base64 bridge. Native requests send no `Origin`, so `SameOriginMiddleware` rejects them while a session cookie is present |
| Webview `fetch` + bearer session token + CORS without credentials | `Authorization: Bearer`, CORS allows the shell origins | **Yes.** Streaming, `FormData`, `AbortSignal` and timeouts keep working exactly as in the browser |

Why bearer + CORS is safe: CORS protects ambient credentials. A bearer token is
not ambient, because a page can only send it if it already holds it. The API
therefore never sends `Access-Control-Allow-Credentials`, and allowing an extra
origin grants nothing to a page without the token. The allow-list stays narrow
anyway:
- `capacitor://app.culina.localhost` on iOS and `https://app.culina.localhost`
  on Android: Capacitor's `server.hostname` sets both. Plain `localhost` is
  avoided because any local dev server shares it.
- The Tauri origins, if part 2 is ever revisited.

### 3.4 Authentication and sessions

Keep one session model; give it a second carrier.

- **Same session row, same token.** Today a session is a row with
  `token_hash = sha256(token)` and the token travels in `__Host-culina.session`.
  A native session is the same row. Only the delivery differs: the token is
  returned in the sign-in response body instead of a `Set-Cookie`.
- **Asking for it.** The sign-in request has an optional `client`: `browser`
  (the default), `ios` or `android`. An app client gets `sessionToken` in the
  response and no cookie. The same field labels the session on the devices
  screen. Sign-in stays one resource (`POST /api/v1/sessions`), per
  `rest-api-design`; it is the only way a session starts.
- **Reading it.** `SessionAuthenticationHandler` accepts
  `Authorization: Bearer <token>` when no session cookie is present. If both
  are present, the cookie wins and the bearer header is ignored, so a browser
  can never be steered onto a token. Renewal slides the row's expiry exactly
  as now, without the cookie rewrite.
- **CSRF.** Requests authenticated by bearer skip `CsrfMiddleware`: forgery
  needs a credential the browser attaches by itself, and a bearer token is
  never attached that way. The client middleware sends `X-Culina-CSRF` only
  when it authenticates by cookie. `SameOriginMiddleware` already applies only
  to requests that carry the session cookie, so it needs no change.
- **Sessions list.** `GET /api/v1/sessions` shows the app as a device. The
  `client` column (`browser` / `ios` / `android`, migration 0041) gives a
  better label than the webview user agent.
- **Expiry and 401.** Unchanged: a 401 still calls `sessionExpired()`. The
  native store deletes the Keychain entry and returns to sign-in, not to
  Connect.
- **Risk accepted.** Once read into memory, the token is reachable by script in
  the webview, which an HttpOnly cookie is not. The bundle is local and signed
  with the app, and the strict CSP and Trusted Types stay in force (2.2), so
  the exposure is script injection into Culina's own code. That is the same
  attack an HttpOnly cookie only partly blunts, since injected script can still
  make requests with it.

### 3.5 The API client layer

The changes needed in `src/frontend/src/lib/api` and the hand-built URLs from
part 1. A build-time constant `__CULINA_NATIVE__` (folded like
`__CULINA_GALLERY__`) keeps all of it out of the web bundle.

| Piece | Web | Native |
| --- | --- | --- |
| `apiOrigin()` (new, `lib/api/instance.ts`) | `''` | The configured origin |
| `createClient({ baseUrl })` in `client.ts` | Relative paths, as now | `apiOrigin()`; `deadlineFor` already parses with `new URL` |
| `events.ts`, `ask.ts` (streamed `fetch`) | `credentials: 'include'` | `credentials: 'omit'`, `Authorization` header |
| Auth middleware (`middleware.ts`) | CSRF header from `culina.csrf` | Bearer header; no CSRF |
| `recipeImage.ts`, `IntakeComparison.svelte`, `ArchivePanel.svelte` | `/api/...` with cookie | See "Images" below |
| Share and invite links (`location.origin`) | Unchanged | `apiOrigin()`: links must point at the instance, never at `capacitor://localhost` |
| `safeRedirect`, telemetry origin filter | Unchanged | Compare against the shell origin, as today |
| Offline reads (service worker `apiCache.ts`) | Unchanged | Same allow-list and owner stamp, as client middleware over IndexedDB |

**Images.** `<img src>` cannot carry an `Authorization` header. Two candidates:

1. **Fetch as blob.** An action or component fetches the image with the
   bearer header and sets an object URL. It is client-only and doubles as the
   offline image cache, but `srcset`'s responsive choice has to be redone by
   hand (pick a `w=` from the element size), and object URLs need revoking.
2. **Signed URLs.** The API returns short-lived `?exp=&sig=` image URLs (HMAC
   over path, width and expiry, key from Data Protection). It is plain `<img>`
   and `srcset` with HTTP caching. But the client cannot build image URLs
   itself any more, the image response's `Cross-Origin-Resource-Policy:
   same-origin` must become `cross-origin` for signed requests, and the
   signature is a credential in a URL (logs, Referer: Culina sends
   `no-referrer`).

**Lean: 1 (fetch as blob)** for the first prototype. It touches no contract,
and it gives the offline image cache the native app needs anyway. Built this
way: the design system's `Image` asks a registered image source
(`$ds/display/imageSources.ts`), which only the app registers
(`lib/app/nativeImages.ts`). It picks the `srcset` width by hand and fetches
through the same offline reads as every other request. Revisit if
memory or scroll performance on long recipe grids suffers. The archive download
(`<a download>`) becomes a fetch plus the native share/save sheet in either
case.

**Backend headers for the native origins** (`SecurityHeaders`): CORS preflight
for the allow-listed origins, allowing `Authorization, Content-Type, If-Match,
If-None-Match, Last-Event-ID, Accept` and exposing `ETag, Retry-After,
Location, X-Request-Id`. Without the expose list, the ETag cache silently stops
working cross-origin. The CSP stays as it is: it governs documents the
instance serves, and the native shell brings its own (2.2). That policy's
`connect-src` cannot name an origin chosen at run time, so it uses
`'self' https:`, plus the opted-in private address if there is one.

### 3.6 Version skew

The bundled UI is built from one commit, while each instance runs whatever its
admin installed. Rules:

- `GET /api/v1/setup` reports `apiVersion`. The app carries the range it
  supports and blocks with "This server is older/newer than this app supports"
  outside it. A banner is not enough: a mismatched UI fails in confusing ways.
- Within `/api/v1`, server changes stay additive (new optional fields, new
  endpoints). The CI `contract` job already diffs `Api.json`. A breaking-change
  check on that diff becomes worth having once a native app exists.
- The app treats unknown enum values and missing optional fields defensively.
  `offline-users-me-outlives-deploys` is the same problem in miniature, and is
  handled the same way.

### 3.7 Streaming in the native shell

Webview `fetch` streams in WKWebView and Android WebView as it does in the
browser, so `eventStream.ts` is unchanged (verify: 2.6 item 5). What differs is
backgrounding. iOS suspends a backgrounded app within seconds, and the stream
dies:

- **Import watch** (`events.ts`) already resumes with `Last-Event-ID` and five
  retries. The app lifecycle `resume` event should re-arm it immediately
  instead of waiting for backoff.
- **AI drafting** (`ask.ts`, `POST /recipe-drafts*`) never resumes, so
  switching apps mid-draft loses the draft. Mobile Safari has the same problem
  today. Options, cheapest first:
  1. Request background time (`beginBackgroundTask`, about 30 s) while a draft
     streams.
  2. Make drafting a durable server job, like photo intakes already are, and
     watch it with `events.ts`.

  Measure how often drafts outlive 30 s before choosing.

### 3.8 Push

Web Push cannot reach a native app, and native push is publisher-bound. APNs
needs a key from the Apple developer account that publishes the app, and FCM
needs that app's Firebase project. A self-hosted server cannot hold either. The
established answer is a **push relay** run by the app publisher: the instance
posts `{device token, payload}` to the relay, and the relay holds the keys
(Nextcloud and Home Assistant both work this way).

That gives Culina an online dependency it does not have today, so:

- It is optional per instance.
- The payload carries only "import ready" and a path, never recipe content.
- Without it, the app falls back to what works today: the import screen shows
  progress while open, and the result is there on return.

Local timer notifications need no push at all (part 4).

### 3.9 Backend changes, summarised

1. Bearer delivery and acceptance of the existing session token (3.4).
2. CSRF skip for bearer-authenticated requests.
3. CORS for a configured allow-list of native shell origins, with the expose
   list (3.5).
4. `apiVersion` on `GET /api/v1/setup`.
5. Session `client` label.
6. Later, optional: push relay sender beside `PushTransport`.

None of these change behaviour for the website: cookie requests take exactly
the path they take today.

---

## 4. Kitchen timers on the lock screen

The web app cannot ring a timer once the browser suspends the page
(`kitchen-pwa-design.md`, "Timer ownership"). That is the most concrete reason
for a native app. This part designs the bridge from the existing timer store
to iOS Live Activities / AlarmKit and Android ongoing notifications.

### 4.1 What the timer store already gives us

`cooking/timers.svelte.ts` and `timerState.ts` are close to what a native
layer needs:
- **Absolute deadlines.** A timer is `{stepIndex, label, endsAt, pausedRemaining?}`,
  with `endsAt` in epoch milliseconds. The OS can count down to a timestamp
  without the webview.
- **One persisted state per cooking session.** Every change goes through
  `queue(id, edit)` → `editTimerState`.
- **Conditional actions.** `applyTimerAction(notice, action)` applies `minute-1`,
  `minute-2`, `dismiss` and `next` only if the timer still has the `endsAt` the
  action was issued for. A stale lock-screen button can never change a newer
  timer. The service worker uses this today, and the native bridge can reuse it
  unchanged.

### 4.2 Bridge architecture

**Declarative down, journal up.** The web side never tells the OS "start" or
"stop". It publishes the whole snapshot whenever it changes, and the native
side reconciles. Actions taken while the webview is asleep go into a native
journal that the web side drains when it wakes.

```
timers.svelte.ts ──snapshot──▶ CulinaTimers.sync()  ──▶ iOS: AlarmKit timers / Live Activity
      ▲                                               ──▶ Android: ongoing + alarm notifications
      │                                                       │ user taps +1 min / Dismiss / Next
      └── applyTimerAction() ◀── CulinaTimers.drain() ◀── native journal (applied natively at once)
```

The contract (TypeScript side, `cooking/nativeTimers.ts`, native builds only):

```ts
interface NativeTimerSnapshot {
  sessionId: string;
  url: string; // the cooking route, for tapping the activity/notification
  recipeTitle: string;
  timers: { stepIndex: number; label: string; endsAt: number; pausedRemaining?: number }[];
}

interface NativeTimerAction {
  sessionId: string;
  stepIndex: number;
  endsAt: number; // the deadline the button was shown for
  action: 'minute-1' | 'minute-2' | 'dismiss' | 'next';
}

interface CulinaTimersPlugin {
  sync(snapshot: NativeTimerSnapshot): Promise<void>; // empty timers ends everything
  drain(): Promise<{ actions: NativeTimerAction[] }>;
  addListener(event: 'action', fn: () => void): Promise<PluginListenerHandle>;
}
```

- **Sync.** One `$effect` in `KitchenRuntime.svelte`, which already owns the
  timer clock, wake lock and badge, calls `sync` with `$state.snapshot` of the
  session's timers. `sync` writes nothing back into state, so it cannot loop
  (`svelte-effect-loops-look-like-rate-limiting`). Ending or abandoning a
  session, sign-out and switching instance all reach `clear()` and sync an
  empty list.
- **Native applies first.** A button press updates the OS presentation at once:
  it reschedules the alarm, updates the activity or notification, and appends
  to the journal. The cook sees the result even when the webview is never
  woken.
- **Drain.** On `resume` and on the plugin's `action` event, the page runs
  `drain()`, applies each action with `applyTimerAction`, then `refresh()`.
  `refresh()` triggers the next `sync`, which converges native state on the
  stored truth. An action whose `endsAt` no longer matches is dropped by
  `applyTimerAction`, exactly as for a stale web notification.
- **Who rings.** In native builds the OS owns alerts. `notifyTimerDone` becomes
  a no-op, since the alarm was scheduled at `sync`. `checkAlarms` plays the
  in-page chime only while the page is visible and the deadline passed within
  the last few seconds. Without that rule, returning to the app would replay
  every alarm the OS has already rung.

### 4.3 iOS

Two ways to present, both from a Swift Capacitor plugin in the checked-in
`ios/` project.

**AlarmKit (iOS 26+): recommended.** `AlarmManager` schedules each Culina timer
as an alarm in `.timer` mode, with a duration measured from `endsAt`.
- What it gives a kitchen timer, without Culina drawing anything:
  - The system rings it through silent mode and Focus, even when the app has
    been terminated.
  - It shows a system-managed Live Activity with a countdown on the Lock Screen
    and Dynamic Island.
  - It supports pause, resume and stop.
- Culina's look comes from `AlarmAttributes` metadata (step label, recipe
  title, tint), plus an optional widget extension for the counting-down state.
- Requirements: `NSAlarmKitUsageDescription` and a one-time
  `requestAuthorization()`, asked on the first timer start, the moment the web
  app asks for notification permission today.
- Extra buttons (+1 min, Next step) are App Intents that write the journal.

**ActivityKit + local notification (iOS 16.2–25, or if AlarmKit's look is
not enough).**
- A Live Activity `CulinaTimerAttributes { sessionId, recipeTitle }` whose
  `ContentState` is the timer list.
- The view renders each running timer with
  `Text(timerInterval: now...endsAt, countsDown: true)`. The system ticks that
  view, and the app never updates it per second.
- Separately, a `UNNotificationRequest` with a
  `UNCalendarNotificationTrigger` at `endsAt` rings. Use the `timeSensitive`
  interruption level if the user allows it; it does not break through silent
  mode, which is the gap AlarmKit closes.
- At zero the countdown text stops at 0:00 and cannot change on its own
  without the app or a push. Set `staleDate = endsAt`, and the view renders a
  "done" state when `context.isStale`.
- Buttons are `LiveActivityIntent`s (iOS 17+). They run in the app's process
  without opening it, update the activity and write the journal.

Layout, for either path (one activity per cooking session, not per timer):

| Surface | Content |
| --- | --- |
| Dynamic Island, compact | Leading: Culina pot glyph. Trailing: soonest timer `m:ss` |
| Dynamic Island, minimal | Soonest timer as a progress ring |
| Dynamic Island, expanded | Step label, countdown, "+1 min" and "Dismiss"; "+N more" when several run |
| Lock Screen | Recipe title, up to three timers (label + countdown or "paused 4:10"), same buttons |

Several concurrent timers are normal while cooking. One activity listing them
reads better than three stacked activities. With AlarmKit, each alarm brings
its own system activity, which is acceptable for two or three timers.
Prototype both and look at them on a phone before choosing.

### 4.4 Android

A Kotlin Capacitor plugin in `android/`. No foreground service: the OS renders
the countdown, so nothing in the app needs to stay alive.

- **Ongoing countdown.** One notification per cooking session on a low-importance,
  silent channel `kitchen-timers`, with `setOngoing(true)`,
  `setUsesChronometer(true)`, `setChronometerCountDown(true)` and
  `setWhen(soonest endsAt)`. The system renders the ticking time. Content shows
  the step label and "+N more", and the actions are **+1 min**, **Dismiss** and
  **Next step**. Tapping it deep-links to the cooking route.
- **Android 16 Live Updates.** Promote the same notification
  (`Notification.ProgressStyle`, `setShortCriticalText` for the status-bar
  chip). It shows as a chip in the status bar, the Dynamic Island equivalent.
  Check `canPostPromotedNotifications()` and fall back to the plain ongoing
  notification.
- **Alarm.** An `AlarmManager` alarm at each `endsAt` posts a high-importance
  `kitchen-alarms` notification with sound and vibration.
  - Exact delivery needs `SCHEDULE_EXACT_ALARM`. The user grants it in
    settings, and it is denied by default for new installs on Android 14+.
  - Without it, `setAndAllowWhileIdle` can fire minutes late in Doze. Ask for
    the permission on the first timer start, with a sentence on why.
  - `USE_EXACT_ALARM` is reserved by Play policy for alarm-clock apps, so do
    not use it.
- **Actions** are `PendingIntent` broadcasts to a `TimerActionReceiver`. It
  reschedules the alarm, updates the notification and appends to the journal
  (`SharedPreferences`), with no webview involved.
- **Reboot.** `AlarmManager` alarms are lost on reboot. A `BOOT_COMPLETED`
  receiver restores them from the last snapshot the plugin stored.
- `POST_NOTIFICATIONS` (Android 13+) is requested at first timer start, as on
  the web.

`@capacitor/local-notifications` covers the alarm half on both platforms. It
could be the first prototype step (4.6), before any custom Kotlin or Swift.

### 4.5 Background lifecycle and battery

- **Countdown rendering costs no app wake-ups.** `Text(timerInterval:)` and
  the Android chronometer are drawn by the system from a timestamp. The webview
  can be suspended or killed; the countdown and the alarm do not depend on it.
- **The JavaScript one-second clock** (`tick`) runs only while the webview is
  in the foreground, as now. It is the in-app display, not the alarm.
- **Wake lock** keeps the screen on while cooking in the foreground (2.6 item 2).
  When the cook locks the phone, the native alarm takes over. That handover is
  the whole point.
- **Expected battery cost** is one scheduled alarm per timer, plus activity
  updates on user actions only. Measure it: Xcode Energy gauge and Android
  Battery Historian over a 60-minute cooking session with three timers and the
  phone locked, against the same session with no timers.

### 4.6 Prototype blueprint and verification

Build in this order. Each step is useful on its own.

1. **Local notifications only** (`@capacitor/local-notifications`, no custom
   native code). `sync` schedules or cancels one notification per timer at
   `endsAt`, with the existing action ids. Proves the bridge, the journal-less
   action path (the plugin delivers `localNotificationActionPerformed` on
   launch) and `applyTimerAction` reuse.
2. **Android ongoing chronometer notification** (Kotlin plugin, 4.4).
3. **iOS AlarmKit timers** (Swift plugin, 4.3), then the custom Live Activity
   if AlarmKit's presentation falls short.

**Status (13 October 2026, `tj23.8`): step 1 is built.**
- `cooking/nativeTimers.ts` implements `sync` over
  `@capacitor/local-notifications`: one notification per (session, step), id
  hashed from both, `extra` is the website's `TimerNotice`.
- `KitchenRuntime.svelte` publishes `$state.snapshot` of the timers from one
  `$effect`. Reconciling leaves unchanged alarms alone.
- The buttons (+1 min, +2 min, Dismiss, Next step) are registered as
  foreground actions and applied with `applyTimerAction`. Foreground, because
  iOS does not wake the web view for a background action; the plugin keeps the
  event until the page listens. That is why the native journal waits for step 3.
- The page refreshes on `culina:timers-changed`. `notifyTimerDone` is a no-op
  in the app, and the in-page chime only sounds for a deadline passed within
  five seconds, on screen.
- Sign-out cancels every alarm (`forgetNativeTimers`).

Steps 2 and 3 replaced it (`tj23.9`); the paragraph above describes the bridge,
which stayed the same.

**Status (13 October 2026, `tj23.9`): steps 2–3 are built.** The app-local
`CulinaTimers` plugin replaced `@capacitor/local-notifications` on both
platforms. One component owns all timer notifications, which matters on iOS,
where only one object can be the notification delegate.
- **The contract** is 4.2's, extended:
  - `sync` also carries the words the system shows, in the app's language.
  - Each journal entry carries `at`, when it was pressed.
    `applyTimerAction(notice, action, at)` uses that, so "+1 min" lands on the
    same deadline on the page as on the lock screen.
  - `open` records a tap; the page drains on start, on `resume` and on the
    plugin's `action` event.
- **iOS** (`native/ios/App/App/CulinaTimers`, `Shared/`, the
  `CulinaTimerWidget` extension):
  - On iOS 26+ with permission, AlarmKit timers with a +1 min secondary button
    and AlarmKit's countdown activity.
  - Otherwise a time-sensitive notification per timer, plus one Live Activity
    per session. Countdowns use `Text(timerInterval:)`, the Lock Screen shows
    up to three timers, and buttons are `LiveActivityIntent`s that run in the
    app's process.
  - Background notification actions (+1/+2 min, Dismiss) are applied without
    opening the app; "Next step" opens it.
  - The AppDelegate claims the notification delegate for background launches.
    Capacitor's router hands it to the plugin once the bridge loads.
  - The app keeps iOS 15 as its minimum and weak-links ActivityKit,
    AppIntents and AlarmKit.
- **Android** (`native/android/.../timers`, Java, since the template has no
  Kotlin):
  - One ongoing notification with the system's countdown chronometer for the
    soonest timer, with "+N more", +1 min, Dismiss and Next step. On Android 16
    it is promoted to a Live Update: API 36 asks for that through the
    `android.requestPromotedOngoing` extra, as AndroidX does.
  - Exact alarms per timer ring on a high-importance alarm channel. The
    exact-alarm settings page is opened once, at the first timer start.
  - Buttons go to a receiver that applies them without the app; "Next step"
    and a tap open the activity.
  - `TimerBootReceiver` rebuilds the alarms after a reboot or update.

Verified on this Mac:
- The Swift typechecks (`swiftc -typecheck`, Swift 5 and 6 modes) against the
  iOS 27 SDK and Capacitor 8.5's frameworks: app at iOS 15, widget at 16.2,
  AlarmKit included.
- The Java compiles with `-Xlint:all` against Android 36's `android.jar` and
  stubs of the Capacitor classes it uses. That compile caught the missing
  `setRequestPromotedOngoing` above.

Not verified, for want of Xcode's simulator plug-in and an Android SDK:
- Linking, resources, the widget target's build, and anything at run time.
  The device checklist below is in `culina-v2-tj23.13`.
- A widget extension also needs its App ID in the developer account, and the
  time-sensitive entitlement needs that capability.

Device checks, iPhone and Android:
- Lock the phone with three timers running, and wait past each deadline. Each
  rings on time and the countdown is correct throughout.
- Force-quit the app, then airplane mode. Alarms still ring and the countdown
  still ticks.
- Tap +1 min / Dismiss / Next step on the lock screen with the app killed.
  Reopen: the page shows exactly the state the buttons produced, and no
  in-app chime replays.
- Pause in the app. The activity or notification shows the paused remaining
  time, and no alarm fires.
- Android: reboot with a timer running; the alarm survives. Deny
  `SCHEDULE_EXACT_ALARM`, and the app explains the late-alarm risk.
- iOS: silent switch on. AlarmKit rings; the plain notification path does not
  (expected, and documented to the user).
- Battery measurement (4.5).

---

## 5. Build pipeline: web and native from one tree

The rule for everything in this part: **`pnpm build`, the Docker image and
the web CI lanes produce exactly what they produce today.** The native bundle
is a second, separate build of the same source into its own directory. It is
never a post-processing step on the web build.

### 5.1 Repository layout

Capacitor resolves native plugin code from the `node_modules` of the package
that holds `capacitor.config.ts`. The web code imports the same plugins' JS
APIs, so splitting the two into separate packages would mean two dependency
lists that drift. One package, `src/frontend`, owns both:

```
src/frontend/
  capacitor.config.ts        appId, webDir: 'build-native', ios.path, android.path, server.hostname
  native/
    ios/                     Xcode project (checked in), incl. CulinaTimers plugin + widget extension
    android/                 Gradle project (checked in), incl. CulinaTimers plugin
  build/                     web output (unchanged, ignored)
  build-native/              native web bundle (ignored)
```

`native/` instead of Capacitor's default top-level `ios/` and `android/`
(`ios.path`/`android.path` in the config) gives the web tooling one directory
to ignore. Culina's own plugins (timers, later the share extension) live as
app-local Swift/Kotlin inside those projects, registered with the bridge. They
are not separate npm packages until something outside Culina wants them.

**Tracked:** both native projects, as Capacitor intends (they are source, edited
in Xcode and Android Studio).

**Ignored** (`src/frontend/.gitignore`):
- `build-native/`
- `native/ios/App/App/public/` and `native/ios/App/App/capacitor.config.json`:
  copies written by `cap sync`
- `native/ios/**/xcuserdata/` and `native/ios/DerivedData/`
- `native/android/.gradle/`, `native/android/**/build/` and
  `native/android/local.properties`
- `native/android/app/src/main/assets/public/`
- `*.jks`, `*.keystore`, `*.p8`, `*.mobileprovision`: signing material never
  enters the repo; gitleaks in the `security` job backs this up

**Trap in the root `.gitignore`.** The generic `data/`, `artifacts/` and
`[Rr]elease/` patterns match at any depth. A Kotlin package directory such as
`native/android/app/src/main/java/app/culina/data/` would be silently
untracked. Anchor those patterns to the root (`/data/`) when `native/` is
added, or avoid those directory names.

**Ignored by the web tooling:**
- eslint `ignores` and `.prettierignore` gain `native/` and `build-native/`.
- vitest's `include` already scans only `src/` and `build-tools/`.
- `svelte-check` does not reach `native/`.

### 5.2 The native web build

`vite.config.ts` reads one variable, `CULINA_TARGET=native` (absent = web, as
today), and changes only these things:

| Setting | Web (unchanged) | Native |
| --- | --- | --- |
| `define` | `__CULINA_GALLERY__` | `+ __CULINA_NATIVE__: true` (false on web, so native code folds away) |
| adapter `pages` / `assets` | `build` | `build-native` |
| Service worker | Built, registered by the app | Never registered (2.2); `finishNativeShell.ts` deletes the built file |
| `siteFiles()` (robots, sitemap) | Yes | No |

Post-build steps differ by target:

| Step | `pnpm build` (web) | `pnpm build:native` |
| --- | --- | --- |
| `addRoutePreloads.ts` | Yes, nonce placeholder for `AppShell` | Code preloads only: the first API requests go to a server chosen at run time, which a preload link cannot name |
| `compressBuild.ts` | Yes (`.br`/`.gz` for Kestrel) | **No**: assets are read from disk; compressed twins only add app size |
| `finishNativeShell.ts` (new) | — | Removes `nonce="__CULINA_NONCE__"`, writes a `<meta>` CSP with sha256 hashes of every inline script and style, `connect-src 'self' https: http:` and `img-src 'self' data: blob:` (photos arrive as object URLs), the same Trusted Types policies as the server's; marks `<html data-shell="culina-app">`; drops the manifest link, `service-worker.js`, `manifest.webmanifest`, `robots.txt`, `humans.txt` |
| `verify:release` | `build` | `build-native` too, with two extra assertions: no `.map` file and no `sourceMappingURL` (CLAUDE.md, never ship source maps, which applies to app bundles too), and no service worker file |

The web build gains one assertion in `verify:release`: no native-only code in
`build/`, found by a string only the app's code holds (`build-tools/assertReleaseContents.ts`).
Connect copy cannot serve, because Paraglide ships every message to both builds.
It is the same technique as the gallery check (`assertNoGallery.ts`), for the
same reason: a leak is invisible otherwise. Both builds are also checked for
source maps.
`verify:budget` keeps measuring `build/` only. If `@capacitor/*` imports are not
behind `__CULINA_NATIVE__`, they would show up there as a budget regression
first.

### 5.3 Scripts and Makefile targets

`src/frontend/package.json`:

```jsonc
"build:native": "CULINA_TARGET=native vite build && node --experimental-strip-types build-tools/addRoutePreloads.ts build-native && node --experimental-strip-types build-tools/finishNativeShell.ts",
"native:sync": "pnpm build:native && cap sync",
"native:ios": "pnpm native:sync && cap open ios",
"native:android": "pnpm native:sync && cap open android",
"native:run:ios": "pnpm native:sync && cap run ios",
"native:run:android": "pnpm native:sync && cap run android"
```

`addRoutePreloads.ts` and `checkRelease.ts` take the build directory as an
argument; `checkRelease.ts` already does.

Root `Makefile` (beside `build`, which does not change):

```make
native-sync:     ## Build the native web bundle and copy it into both native projects
	cd src/frontend && pnpm native:sync
native-ios:      ## Open the iOS project in Xcode
	cd src/frontend && pnpm native:ios
native-android:  ## Open the Android project in Android Studio
	cd src/frontend && pnpm native:android
```

Development against a running API: `cap run` with Capacitor's live reload
(`server.url` pointing at the Vite dev server) is for UI iteration only. It
serves from `http://<lan-ip>:5173`, which is a different origin from the
packaged app, so cookie and auth behaviour must be checked on a real
`build:native` before believing it. The connect screen (3.2) takes the
developer's LAN API (`make dev` already binds `LAN=1`).

**Toolchain prerequisites** (documented in `docs/deployment.md` or a
`native/README.md`):
- Xcode with simulators: `sudo xcodebuild -runFirstLaunch` (CoreSimulator was
  missing on the planning Mac).
- Android Studio, JDK 21 and an Android SDK.
- No CocoaPods: Capacitor 8 uses SPM.

### 5.4 CI and release

`ci.yml` jobs stay as they are. Additions:

| Job | Runner | When | Does |
| --- | --- | --- | --- |
| `frontend` (existing) | ubuntu | every push/PR | + `pnpm build:native` + `verify:release build-native`, cheap enough to run always, and catches a native-only type or build break on the PR that causes it |
| `native-android` (new) | ubuntu, JDK 21 | PRs touching `src/frontend/**`, pushes to main | `pnpm native:sync`, `./gradlew lint testDebugUnitTest assembleDebug` in `native/android` |
| `native-ios` (new) | macos | pushes to main, nightly, manual | `pnpm native:sync`, `xcodebuild build` for the iOS Simulator destination, `xcodebuild test` for the Swift plugin's unit tests (timer reconciliation, journal) |

- **Why iOS is not on every PR:** macOS runner minutes cost about ten times
  Linux minutes on private repositories. The Swift code changes rarely; a
  break surfaces on main within one push. Revisit if that proves too late.
- **`publish` gets no new dependency.** A native lane failure must not block
  the server image that self-hosters depend on.
- **Testing layers.** The UI is the same Svelte code, so Playwright on the web
  build stays the main UI test, and native code gets unit tests in its own
  language. End-to-end native smoke tests come later: Maestro flows (launch,
  connect, sign in, start a timer, lock, see the notification) on an Android
  emulator (`reactivecircus/android-emulator-runner` on ubuntu with KVM), then
  iOS simulator. The things that matter most (lock-screen timers, keyboard,
  WKWebView quirks) need the device checklist in 2.6 and 4.6 before each store
  release anyway.
- **Release** (`native-release.yml`, on `v*` tags after `release.yml`'s gate):
  - Android: signed AAB to the Play internal track.
  - iOS: archive and upload to TestFlight with an App Store Connect API key.
  - Signing secrets live in a GitHub environment with required reviewers.
  - The version comes from the tag, and the build number from the run number.
  - Store promotion stays a manual step.
- **Dependabot** gains the `gradle` and `swift` ecosystems for `native/`. The
  security job's `pnpm audit` already covers `@capacitor/*`.
- **`scripts/ci-local.sh`** frontend lane adds `build:native` and its
  `verify:release`. Gradle and Xcode stay out of the local CI mirror.

### 5.5 Web parity checklist

What must be true when the first native commit lands, and stay true:

- `.dockerignore` gains `src/frontend/native` and `**/build-native`.
  - Without them, `COPY src/frontend/ ./` (Dockerfile:41) pulls the Xcode and
    Gradle projects into the frontend stage. They would not ship, since only
    `build/` is copied onward. But they bloat the context and invalidate the
    layer cache on every native edit.
- `pnpm build` output carries no app code (`verify:release`), the same
  service worker and the same `.br`/`.gz` set. The shared client modules did
  change, so hashes differ; what must not differ is behaviour on the website,
  which the unit and e2e suites hold.
- `verify:release` (no gallery, no native code) and `verify:budget`
  (first-load 80 kB) pass on `build/`.
- `__CULINA_NATIVE__` is a literal `false` in web builds, so every native
  branch is dead code the bundler drops, as with `__CULINA_GALLERY__`.
- No CORS, no bearer path and no CSRF difference for browser requests (3.9):
  cookie requests take today's path.

---

## 6. First steps

The planning ends here. These are the concrete next pieces of work, in order,
each tracked as a bead under `culina-v2-tj23`:

1. **Backend: native session carrier** (`tj23.5`). Bearer delivery and acceptance of the
   session token, CSRF skip for bearer, CORS allow-list with exposed headers,
   `apiVersion` on `GET /setup`, and the session `client` label (3.9).
   Testable entirely with integration tests, with no native project needed.
2. **Frontend: instance-aware API client** (`tj23.6`). `apiOrigin()`, `baseUrl`, bearer
   middleware, image fetch, share-link origin and the connect/verify flow
   behind `__CULINA_NATIVE__` (3.2, 3.5), plus `build:native` and the CI/Docker
   parity checks (5.2, 5.5).
3. **Capacitor shell prototype** (`tj23.7`). `native/ios` and `native/android`; run the
   2.6 checklist on devices.
4. **Native timers, step 1** (`tj23.8`). Local notifications through the snapshot bridge
   (4.6 step 1).
5. **Native timers, steps 2–3** (`tj23.9`). Android ongoing notification; iOS AlarmKit /
   Live Activity.
6. **Later.** Push relay (3.8, `tj23.10`), durable AI drafts (3.7, `tj23.11`),
   native CI lanes and Maestro smoke tests (5.4, `tj23.12`), share extension.

---

## 7. Push relay

The website tells its owner "your recipe is ready" with Web Push. The server
signs with its own VAPID key (`Infrastructure/Import/PushTransport.cs`) and
delivers from a durable outbox (`recipe_intake_notifications`, claimed,
retried for 12 attempts). An installed app cannot be reached that way:

- APNs accepts only the key of the Apple developer account that publishes the
  app.
- FCM accepts only that app's Firebase project.

A self-hosted server holds neither, and must not be given them. So the app's
publisher runs a small relay that holds those keys and forwards (3.8). This part
designs it so the relay learns as little as possible and each instance opts in.

### 7.1 Shape: the relay knows devices, not servers

```
app ──(1) device token──▶ relay            relay ──▶ APNs / FCM ──▶ phone
 │◀──(2) relay token─────┘                    ▲
 └──(3) relay token──▶ instance ──(4) {relay token, text, path}──┘
```

1. The app gets its APNs or FCM device token. It sends the token to the relay:
   `POST /v1/devices {platform, deviceToken}`.
2. The relay answers with a **relay token**: 32 random bytes, which the relay
   maps to that device. The relay is never told which server or person the
   device belongs to.
3. The app registers the relay token with its own server, beside the Web Push
   subscriptions it already has: `PUT /api/v1/push/devices/{deviceId}`
   `{relayToken, language}`, and `DELETE` on sign-out. Ten per person, the same
   cap as `web_push_subscriptions`.
4. The server's outbox delivers to devices as it does to subscriptions:
   `POST {relay}/v1/notifications` `{relayToken, title, body, path, tag}`.

Holding a relay token is the permission to push to that device. That is the
Web Push model, where holding the endpoint URL is the permission. It is kept on
the device and on the one server the person signed into. The relay limits each
token (30 an hour) and each source address, so a leaked token can only annoy,
not flood. The app revokes its token on sign-out (`DELETE /v1/devices/{relayToken}`)
and gets a new one on the next sign-in.

### 7.2 What crosses the relay

- **Text.** The words the person sees ("Your recipe is ready" / "Dein Rezept
  ist bereit"), in the language the device registered with. The server
  composes them, as `IntakeNotifications.DeliverAsync` does today.
- **A path** (`/recipes/imports/{jobId}`) for the tap to open, and a **tag** so
  a repeat replaces rather than stacks.
- **Never** a recipe title, an ingredient, a photo, a person's name or the
  server's address.
- The relay keeps counters (accepted, delivered, refused, rate-limited). It
  keeps no payload log.

### 7.3 On the server

- **Opt-in, off by default.** An admin setting `Push:RelayUrl` (admin-editable,
  `dotnet-configuration`) turns it on. The admin screen says that turning it on
  sends notification text to a service outside this server, and names it.
  Without it, the app gets no push, and the import screen still shows progress
  while open (3.8).
- **Same outbox.**
  - A `native_push_devices` table (`device_id`, `user_id`, `relay_token`,
    `language`, `created_at`); `recipe_intake_notifications` gains rows for
    devices.
  - The existing claim/retry/attempts loop delivers to both kinds through a
    second transport beside `PushTransport`.
  - It reuses that transport's public-address-only HTTP handler: the relay URL
    is admin-supplied, but the same guard costs nothing.
- **A relay answering 410** means the device revoked its token or the store
  dropped it. The device row is deleted, as a 404/410 deletes a Web Push
  subscription today.

### 7.4 In the app

- `@capacitor/push-notifications` supplies the token:
  - iOS needs the `aps-environment` entitlement.
  - Android needs the publisher's `google-services.json`, which is not
    committed. CI writes it from a secret, as signing material.
- The token is asked for only when the person turns on "Tell me when an import
  is ready". This is the same moment the website asks for notification
  permission (`features/import/push.svelte.ts`, which today reports
  `supported: false` in the app).
- Tapping a notification opens its path through the plugin's
  `pushNotificationActionPerformed`.
- It must coexist with the timers' notification delegate on iOS. Capacitor's
  router keeps push and local notifications apart
  (`pushNotificationHandler` / `localNotificationHandler`), and `CulinaTimers`
  holds only the local slot.

### 7.5 The relay itself

A small stateless HTTP service: two endpoints, a key-value store (relay token
→ platform and device token), and APNs (token-based `.p8` key) plus FCM HTTP v1
clients. Its operation belongs to whoever publishes the app, not to Culina's
server image. It is deployed and monitored separately, and the server treats
it like any other optional outbound dependency: an outage only delays
"import ready" notifications.

Tracked: `culina-v2-tj23.14` (the relay), `culina-v2-tj23.15` (server devices
and outbox), `culina-v2-tj23.16` (app registration).

