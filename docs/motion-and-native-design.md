# Motion, View Transitions & Native Tactile Polish

Specification for UI choreography, View Transitions, and tactile feedback across web and Capacitor (`src/frontend/src/app.css` & `src/frontend/src/lib/native/haptics.ts`).

---

## 1. Motion Principles

- **Purposeful & Interruptible:** Animations communicate spatial origin (e.g., a drawer sliding up from a bottom trigger, a checked shopping item collapsing smoothly into the completed section) and never block user input.
- **Fast Kitchen Timings:**
    - Micro-interactions (button press `scale(0.98)`, pill toggle, checkbox tick): `140ms` (`--duration-fast`).
    - Drawers, popovers, and modals: `220ms` (`--duration-normal`) with `--ease-out-expo` (`cubic-bezier(0.16, 1, 0.3, 1)`).
    - Route & layout transitions: `260–360ms` (`--duration-slow`).
- **Reduced Motion Compliance:** `@media (prefers-reduced-motion: reduce)` disables all spring/slide transforms and reduces durations to instant opacity crossfades.

---

## 2. View Transitions & Shared Elements

- **SvelteKit `onNavigate` Hook:** Wraps route changes in `document.startViewTransition` when supported by the browser/WebView, keeping the navigation shell (`Sidebar` / `BottomNav` / `FloatingTimerBar`) stationary while content crossfades.
- **Shopping List Check-Off Choreography:** Checking an item triggers a `140ms` strike-through + sage checkbox fill + `hapticLight()`, waits `180ms` so the eye registers completion, and smoothly animates the row into the collapsed `Erledigt` group.

---

## 3. Native Touch & Gesture Polish

- **Tap Highlight & Selection Suppression:** Interactive controls set `-webkit-tap-highlight-color: transparent` and `user-select: none` while keeping recipe text, ingredient lines, and notes fully selectable.
- **Safe-Area & Keyboard Choreography:** Bottom sheets pad by `env(safe-area-inset-bottom)` and shift smoothly with `--keyboard-inset-height` on iOS.
