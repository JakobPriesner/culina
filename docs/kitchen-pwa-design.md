# Kitchen notifications, badges and lighting

Phase 1 mapping for culina-v2-nb65.1 and culina-v2-nb65.7, finalized 2026-10-05.

## Badge ownership

A single badge manager resolves producers: active running kitchen timers > remaining shopping items in the selected household > clear. Counts are not added together. Expired timers remain visible in the kitchen but do not count as running. Dismissal, session completion, household switches and sign-out recompute or clear the badge. Shopping uses the latest list already loaded by the app, without extra background API polling; unknown lists contribute zero. Household alerts have no badge producer until there is a defined unread resource and acknowledgement policy.

Updates are serialized and coalesced so a slow badge request cannot overwrite a later clear. Unsupported APIs and permission failures are silent. Service worker timer actions update the same badge precedence from persisted timer and shopping counts.

## Timer ownership

Timers use absolute deadlines, identified by cooking session and step, in IndexedDB shared by windows and the service worker. Existing localStorage timers migrate on first load; storage failures retain working in-memory timers. Actions are conditional on the notification's deadline so an old alert cannot dismiss or extend a replacement timer. +1 min and +2 min restart from now; Dismiss removes that timer; Next step removes it and queues the next step for the matching session, applied by the app when available. Actions notify open windows without focusing them. The notification body opens the matching cooking route. A pending next step survives closing the app and is clamped against the loaded recipe before applying. Notification tags include session and step, with renotify for each new deadline. Platforms may limit the number of action buttons; extension actions are first, and the app retains all controls.

The app shell runs the timer clock for the lifetime of the cooking session, including other routes. Foreground alarms use the unlocked kitchen chime and haptic cadence; background notifications request OS sound/vibration. Notification permission is requested only from starting a timer.

Web platforms do not provide a reliable local alarm scheduler after a browser suspends or closes every window. IndexedDB preserves deadlines and notification actions, but cannot wake a service worker at an arbitrary time. Timers reconcile immediately on return. Web Audio and navigator.vibrate cannot run in service workers. Guaranteed alarms with no live app would require a separate server-push or native scheduling design; no long-lived worker timeout pretends to provide this guarantee.

## Lighting and wake lock

Normal uses the chosen theme. Glare uses opaque white surfaces, dark text and strong borders for bright countertops, targeting at least 7:1 for body and muted text. OLED uses true black page and panel surfaces, subdued light text, no decorative shadows, and opaque chrome for long cooking sessions; this can reduce OLED pixel power, but does not guarantee a battery saving. Neither mode is selected by a sensor or silently changes the user's regular theme. The device remembers the choice, applies it only during an active cooking session, and restores the regular theme when cooking ends. Forced-colour and print modes retain their own controls.

Wake lock is held while the session is active, reacquired on return to the foreground, and released when it ends. The NowCookingBar and cooking controls expose a steady, labelled indicator of the actual held state, including browser refusal or release. There is no flashing or continuous animation.

## Cooking remote

Media Session controls are an optional tool beside lighting and the floating cooking window. A direct tap enables a ten-second silent PCM WAV loop from the same origin. The WAV is a bundled asset, compatible with the document CSP and installed by the service worker; range requests for audio can use the cached full response offline. Enabling is scoped to the current cooking session and device; it is not remembered as a preference and does not automatically take audio focus on arrival. The floating window offers the same toggle; the ongoing cooking bar lets the cook turn it off while browsing elsewhere.

The shell runtime owns it alongside the timer clock and wake lock. Leaving the recipe route keeps the remote alive, with the cooking recipe retained even when another recipe is loaded. Next/Previous use the shared cooking store, clamp at the recipe bounds, and never silently record completion. Metadata includes recipe artwork, current step, plain instructions using the surface's Markdown parser and ingredient scaling, and the selected timer. Step, yield, artwork and pause changes publish immediately; countdown metadata updates at most every ten seconds.

Play/Pause selects the current step's live timer first, then the soonest running timer, then a paused timer. Play stays paired with a fallback timer paused by the remote until the step changes. Expired timers are not restarted. With no live timer it pauses/resumes the silent audio session without starting a timer or ending cooking. Speech narration is tracked separately in nb65.3 and is not yet available. Browser audio interruptions release remote controls while cooking timers continue; an explicit Pause media action pauses the selected timer. The existing timer notifications, badges and floating view all observe the same persisted timer state.

Turning off the remote or receiving the OS Stop action releases only the audio session. Ending/abandoning cooking, switching sessions, signing out, unmounting the shell or hiding the document for navigation releases audio, handlers and metadata. Browser refusal leaves a retryable button; missing support hides the tool. This uses standard media controls, not a native watch app or Live Activity: lock-screen presentation, Dynamic Island behavior and the particular headset/watch buttons depend on the operating system and browser. Hardware verification remains necessary on supported phones and paired watches; browser automation verifies dispatch and shared state, not an OS widget.

## Platform references

- [Notification actions and platform limits](https://developer.mozilla.org/en-US/docs/Web/API/Notification/actions)
- [Service worker notifications](https://developer.mozilla.org/en-US/docs/Web/API/ServiceWorkerRegistration/showNotification)
- [Background operation and worker lifetime](https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps/Guides/Offline_and_background_operation)
