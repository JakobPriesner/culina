# Navigation Architecture Research & Rationale

Summary of the information-architecture decisions behind Culina's 5-tab navigation shell (`Heute`, `Sammlung`, `Woche`, `Einkauf`, `Tagebuch`).

---

## 1. Why 5 Top-Level Destinations

Earlier iterations had 7+ top-level links (`Dashboard`, `Rezepte`, `Kochbücher`, `Favoriten`, `Wochenplan`, `Einkaufsliste`, `Vorrat`, `Tagebuch`), which overflowed mobile bottom navigation bars and fragmented closely related tasks.

Consolidating into **5 core jobs-to-be-done** solved both mobile ergonomics (`BottomNav.svelte`, max 5 tabs at `>= 44px` width) and mental model clarity:

1. **Heute (`/`):** _"What is happening in the kitchen right now?"_ (Today's meals, active timers/cook session, expiring pantry items).
2. **Sammlung (`/rezepte`):** _"What can we cook?"_ (All recipes, favorites filter, manual & smart **Kochbücher**, and connected libraries unified under one roof).
3. **Woche (`/woche`):** _"What are we eating this week?"_ (7-day planner + templates).
4. **Einkauf (`/einkaufsliste` & `/kueche`):** _"What do we have vs. what do we need to buy?"_ (Shopping list paired with segmented switch to **Küche / Vorrat**).
5. **Tagebuch (`/tagebuch`):** _"How am I nourishing myself?"_ (Personal food log, macros, weight, water).

---

## 2. Key Rejected Alternatives

- **Hamburger Drawer on Mobile:** Rejected because switching between `Sammlung`, `Woche`, and `Einkauf` happens dozens of times per week and requires 1-tap thumb reach.
- **Floating Action Button (FAB) Stack on Every Screen:** Replaced by contextual header actions (`+ Rezept` in Sammlung, `+ Hinzufügen` per slot in Woche/Tagebuch, sticky quick-add bar in Einkauf) so floating chrome never obscures list items.
- **Separate Top-Level Tab for `Kochbücher`:** Merged into `Sammlung` as a top filter rail / pill selector (see [`docs/library-navigation-design.md`](library-navigation-design.md)).
