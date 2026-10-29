# Native App Architecture (iOS & macOS)

Specification for Culina's Capacitor 8 native wrapper targeting iOS (`17.0+`) and macOS via Mac Catalyst (`src/frontend/ios/`, `src/frontend/capacitor.config.ts`, and `src/frontend/src/lib/native/`).

---

## Part 1. Platform Strategy & Architectural Principles

- **Single SvelteKit Codebase (`adapter-static`):** The same static SPA bundle served by the Docker container is packaged into the native iOS/macOS app shell (`webDir: 'build'`).
- **Self-Hosted Instance Discovery:** Because every user hosts their own Culina server (`https://culina.example.com`), the native app boots into a first-run **Server URL Connect** screen (`nativeServerStore`), verifies `GET /healthz`, and persists the chosen instance origin in native storage before loading `/login`.
- **Progressive Native Enhancement (`isNativePlatform()`):** All native plugins reside behind thin TypeScript facades in `src/frontend/src/lib/native/` that no-op or fall back to standard Web APIs in browsers.

---

## Part 2. Authentication, Networking & Instance Binding

### 2.1–2.2 Token Storage & Keychain Bridge (`native-auth.ts`)

- In web browsers, the refresh token lives in an `HttpOnly` cookie (`culina_rt`).
- On native Capacitor (`capacitor://localhost`), cross-origin cookie partitioning can drop cookies across app restarts. The native auth layer stores the session credential and instance URL via `@capacitor/preferences` / Keychain-backed storage and attaches `Authorization: Bearer <token>` via the API client interceptor (`src/frontend/src/lib/api/client.ts`).

### 2.3–2.6 Native Passkeys & Deep-Link Origin Validation

- WebAuthn passkeys work across web and native via Associated Domains (`webcredentials:<instance-domain>`).
- Deep links (`culina://` custom scheme and Universal Links `/shared/*`, `/recipes/*`) are routed by `@capacitor/app` (`appUrlOpen`) inside `native-lifecycle.ts` after validating the path against internal SvelteKit routes.

---

## Part 3. Native Capabilities & Plugin Bridges (`src/frontend/src/lib/native/`)

### 3.1 Platform Detection (`platform.ts`)

Exports `isNative()`, `isIOS()`, `isMacCatalyst()`, and `getPlatform()` using `@capacitor/core`.

### 3.2 Haptics (`haptics.ts`)

Wraps `@capacitor/haptics`:

- `hapticLight()` — Shopping list item check/uncheck, portion stepper tap, filter pill toggle.
- `hapticMedium()` — Meal plan drag-and-drop snap, timer start/pause, barcode detected.
- `hapticSuccess()` / `hapticWarning()` — Recipe cooked completion, timer alarm, destructive action warning.

### 3.3 Native Barcode Scanner (`barcode.ts`)

- On iOS, invokes `@capacitor-mlkit/barcode-scanning` (Apple Vision / AVFoundation camera sheet) for instant EAN-13 / EAN-8 / UPC-A scanning in low light.
- On web or Mac Catalyst without MLKit, falls back transparently to `BarcodeScannerModal.svelte` (`BarcodeDetector` API → `zxing-wasm`).

### 3.4 Native Share Sheet & Receive Share Extension (`share.ts`)

- **Outbound (`@capacitor/share`):** Shares recipe links, formatted markdown ingredient lists, or exported files via the system UIActivityViewController.
- **Inbound Share Target:** Accepts URLs and images shared from Safari/Photos into Culina's `/rezepte/neu` intake flow via `culina://intake?url=...`.

### 3.5 Cooking Mode Keep-Awake & Local Timer Notifications (`keep-awake.ts`, `notifications.ts`)

- **Screen Wake Lock (`@capacitor-community/keep-awake`):** Prevents screen dimming while `CookingMode.svelte` is active, falling back to `navigator.wakeLock` on web.
- **Background Cooking Timers (`@capacitor/local-notifications`):** Schedules exact OS-level local notifications (`scheduleTimerNotification`, `cancelTimerNotification`) whenever a step timer starts so alarms fire reliably even if the iPhone is locked or another app is foregrounded.

### 3.6 Native Speech Synthesis (`tts.ts`)

- Powers hands-free step readout in `CookingMode.svelte` (`de-DE` voice selection, rate `0.95`), using native `AVSpeechSynthesizer` / Web Speech API with clean cancellation on step change.

---

## Part 4. Offline Cache & Optimistic Sync Queue (`offline/`)

### 4.1–4.2 Offline Read Cache (`offline-cache.ts`)

- Persists recently viewed recipes, the current week's `MealPlan`, and the primary `ShoppingList` in IndexedDB / `@capacitor/preferences` so users have full access in supermarket dead zones.

### 4.3–4.4 Offline Shopping Mutation Queue (`offline-queue.ts`)

- Checking/unchecking shopping items or adding manual items while offline (`!navigator.onLine`) appends mutations to a durable FIFO outbox and updates local state optimistically.
- When connectivity returns (`@capacitor/network` `networkStatusChange`), the queue replays mutations sequentially with `Idempotency-Key` headers and reconciles against the live SSE stream.

### 4.5–4.6 Network Status Banner (`OfflineBanner.svelte`)

- Displays a non-intrusive warm banner (`"Offline-Modus – Änderungen werden beim Verbinden synchronisiert"`) when disconnected.

---

## Part 5. iOS & iPadOS Layout Adaptations

- **5.1 Safe Area Insets:** Shell headers, bottom navigation (`BottomNav.svelte`), floating timer bar (`FloatingTimerBar.svelte`), and bottom drawers respect `env(safe-area-inset-top)` and `env(safe-area-inset-bottom)`.
- **5.2 Keyboard Handling (`@capacitor/keyboard`):** Adjusts scroll padding in drawers (`FoodSearchSheet`, `QuickAddInput`, Olli chat) when the virtual keyboard appears.
- **5.3–5.4 Rubber-Band Overscroll & Swipe Back:** Native WebView bounce is preserved on primary scroll containers while prevented on fixed sheets; iOS interactive edge-swipe back gesture (`allowsBackForwardNavigationGestures = true`) is enabled in `MainViewController`.
- **5.5 System Theme & Status Bar (`@capacitor/status-bar`):** Syncs status bar style (`Light` / `Dark`) dynamically with Culina's Warm Parchment / Warm Espresso theme.

---

## Part 6. System Integration (Widgets, Shortcuts & Spotlight)

- **App Shortcuts / Siri Intents:** Exposes quick launch deep links (`culina://shopping`, `culina://diary?action=scan`, `culina://today`, `culina://recipes/new`).
- **Home Screen Quick Actions:** 3D Touch / long-press app icon shortcuts for _Einkaufsliste_, _Barcode scannen_, _Wochenplan_, and _Neues Rezept_.

---

## Part 7. macOS Native Target (Mac Catalyst)

- **7.1–7.2 Window & Sidebar Geometry:** Runs as a native macOS window via Mac Catalyst (`SUPPORTS_MACCATALYST = YES`, minimum window size `900×640`) using the desktop persistent sidebar layout (`>= 1024px` breakpoint) and hidden titlebar inset (`titlebarAppearsTransparent`).
- **7.3 Pointer & Hover Polish:** Disables touch-only swipe hints on desktop pointer devices (`@media (hover: hover) and (pointer: fine)`).
- **7.4–7.5 Menu Bar & Keyboard Shortcuts (`Cmd+K` search/Olli, `Cmd+N` new recipe, `Cmd+1..5` primary tabs, `Cmd+,` settings):** Mapped in `src/frontend/src/lib/native/mac-shortcuts.ts`.

---

## Part 8. Build, Sync & Xcode Workflow

```bash
# Build static SvelteKit bundle and sync into iOS/macOS Xcode project
pnpm --dir src/frontend build:native   # sets CAPACITOR_BUILD=true && vite build && cap sync ios

# Open in Xcode (iOS Simulator, physical iPhone/iPad, or My Mac Catalyst)
pnpm --dir src/frontend cap:open:ios
```

- **8.1–8.2 Verification:** `src/frontend/src/lib/native/*.test.ts` unit-tests all native bridges, offline outbox replay, and platform guards under Vitest.
