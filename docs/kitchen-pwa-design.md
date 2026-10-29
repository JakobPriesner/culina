# Kitchen Mode, PWA & Hands-Free Cooking Design

Specification for Culina's progressive web app capabilities, step-by-step cooking mode (`CookingMode.svelte`), timers (`FloatingTimerBar.svelte`), and voice readout.

---

## 1. Step-by-Step Cooking Mode (`CookingMode.svelte`)

- **Distraction-Free Kitchen Surface:** Full-viewport overlay with enlarged typography (`--text-xl` instructions), `48×48px` minimum touch targets for wet/floury hands, and keyboard/swipe step navigation (`←` / `→` / `Space`).
- **Step-Scoped Ingredient Chips:** Each step displays the exact scaled quantities of ingredients referenced in that step (`StepIngredients`), plus a collapsible full ingredient checklist drawer.
- **Server-Synced Session (`ActiveCookSession`):** Current step, checked ingredients, portion scale, and running timers persist to `/api/v1/recipes/{id}/cook-session` so a cook session started on a desktop or iPad can be resumed on an iPhone at the stove.

---

## 2. Concurrent Timers, Wake Lock & Push Notifications

- **Inline Duration Detection:** Steps with `DurationMinutes` (or parsed time expressions like `"12 Minuten köcheln"`) render a one-tap timer pill directly inside the step card.
- **Multi-Timer Bar (`FloatingTimerBar.svelte`):** Supports multiple named concurrent timers ticking simultaneously, visible across all routes if the user leaves cooking mode to check `Woche` or `Einkaufsliste`.
- **Screen Wake Lock:** Acquires `navigator.wakeLock.request('screen')` on web and `@capacitor-community/keep-awake` on iOS while cooking mode is open; automatically re-acquires on `visibilitychange`.
- **Background Alarms:** Fires WebPush notifications (`/api/v1/push/*` via `sw.js`) on web PWA and `@capacitor/local-notifications` on native iOS + Web Audio API chime when a timer reaches `00:00`.

---

## 3. Hands-Free Voice Readout (TTS)

- **Step Readout (`tts.ts`):** Tapping the speaker button in `CookingMode` reads the active step's ingredients and instruction aloud in German (`de-DE`) using the Web Speech API (`speechSynthesis`) or native iOS speech synthesizer, automatically cancelling speech when advancing steps or closing cooking mode.

---

## 4. PWA Manifest & Service Worker (`static/manifest.webmanifest`, `sw.js`)

- **`display: standalone`**, theme colour `#FAF7F2` (light) / `#141210` (dark), maskable icons, and Web Share Target registration (`/rezepte/neu?shared=...`) so URLs shared from mobile browsers open directly in Culina's recipe importer.
