# Culina motion and native design

Reviewed 6 October 2026. Preserve the warm editorial typography, kitchen colours,
photography and Olli. Polish comes from coherent behaviour and stable hierarchy.

## Research and design decisions

[Apple's motion guidelines](https://developer.apple.com/design/human-interface-guidelines/motion)
recommend purposeful, optional motion and predictable relationships between an
entrance and its dismissal.
[Designing Fluid Interfaces](https://developer.apple.com/videos/play/wwdc2018/803/)
emphasizes immediate response, spatial consistency and interruptible behaviour.
For Culina this means quick press feedback, a sheet returning toward the bottom,
and navigation that can supersede a transition already in progress.

[Material's motion system](https://m3.material.io/styles/motion) distinguishes
standard and expressive motion. Culina uses restrained surface transitions;
Olli's existing springs provide the expressive character movement. The durations
below are Culina's choices, not claimed Apple or Material specifications.

[Browser animation guidance](https://web.dev/articles/animations-guide) recommends
transform and opacity for efficient motion. The previous streaming blur, rotating
gradient, morphing orb and moving text gradient produced competing visual signals
and recurring paint work. The new generation frame has still colours, status
copy stays plain, and completed fields arrive with a short fade and 4px travel.
The optional status light pulses twice and settles; Olli's work loop is visible,
pausable and respects reduced motion.

## Implemented system

| Interaction | Behaviour | Timing |
| --- | --- | --- |
| Button press | Subtle compression, no layout change | 120ms |
| Page change | Content dissolves; shell controls stay steady | 120ms out / 200ms in, no delay |
| Filters, quantities, URL fragments | Update in place | No page transition |
| Modal | Small rise and scale, matching return | 280ms enter / 180ms exit |
| Mobile sheet | 32px rise from bottom, no scaling | 280ms enter / 180ms exit |
| Popover | Fade without changing measured geometry | 120ms |
| Streaming recipe fields | Fade and 4px rise, no animated blur | 200ms |
| AI improvement | Larger Olli thinking then writing, readable status; motion controlled in Settings | Existing interruptible springs |
| Background import | Larger work scene, clearer current/completed stages | Real server stages |

Overlay transitions progressively enhance the native dialog and popover APIs with
[`display` and `overlay` discrete transitions](https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Properties/overlay).
Native closing, focus restoration and Escape handling remain immediate. Browsers
without the necessary support use the existing immediate close. Reduced motion
removes travel and transition delays. Olli always stays visible. Settings disables mascot and AI background motion; legacy hidden preferences migrate to stillness. The drawing scene uses a 28-second phrase of colour selection, brisk strokes, inspection and detailing, retaining the picture on later cycles.

The design gallery at `/design#ai-motion` contains a live improvement specimen.
It remains excluded from release builds. Browser tests cover focus, dismissal,
reopening, narrow reflow, mascot preferences and reduced motion.

## Capacitor direction

This change improves the shared web UI. It does not install Capacitor or produce
an iOS/Android binary. Native polish requires device work beyond wrapping the site.

The current frontend configures the SvelteKit static adapter in `src/frontend/vite.config.ts`, making its compiled assets
a suitable starting point for the
[Capacitor build/sync workflow](https://capacitorjs.com/docs/basics/workflow).
The native build needs an explicit deployment design: packaged assets, a configured
HTTPS API origin, existing cookie/CSRF authentication, and compatibility with the
backend's security policy. Relative `/api` calls cannot assume a web-server proxy
inside a packaged app. Verify this architecture before adding platform projects.

Give one layer ownership of safe-area insets so the header, sheets and bottom
navigation do not double-pad the notch or home indicator. Keep dynamic viewport
heights, real touch targets, readable text and zoom accessibility. Match system
bars to the selected theme and test rotation, large text and keyboard appearance.

Use the [Keyboard plugin](https://capacitorjs.com/docs/apis/keyboard) to coordinate
viewport resizing and keyboard events with forms and pinned actions. Test on actual
iPhones: browser emulation cannot establish keyboard or WKWebView behaviour.
Use [App lifecycle and back-button events](https://capacitorjs.com/docs/apis/app)
to resume server progress on foregrounding and dismiss an open surface before
navigating backward on Android. Preserve screen state across background/resume.

Introduce native capabilities through narrow platform adapters: camera/photo
selection, selective haptics for confirmations, native push and deep links, and an
iOS share extension for incoming recipes. The existing iPhone PWA copy-link fallback
continues to matter. A native shell alone does not create an incoming share extension.

Validate the complete journey on iOS and Android devices: launch, sign-in,
navigation, keyboard/form editing, recipe import and improvement, backgrounding,
offline cooking, push/deep-link return and accessibility. Profile frame pacing on
an older supported phone before describing the native experience as finished.
