# Recipe Detail Actions & Toolbar Hierarchy

Specification for the action hierarchy on `/rezepte/[id]` (`src/frontend/src/routes/rezepte/[id]/+page.svelte`).

---

## 1. Three-Tier Action Hierarchy

To prevent toolbar clutter on recipe detail pages, actions are divided into three tiers by kitchen frequency:

### Tier 1 — Primary Kitchen Actions (Always Visible)

1. **Kochmodus starten (`Primary CTA`):** Opens distraction-free `CookingMode.svelte` (or resumes an active `ActiveCookSession` if one is in progress).
2. **Einplanen (`Woche`):** Opens the quick day/slot popover to schedule the recipe into the current or next week at the currently selected portion count.
3. **Auf Einkaufsliste:** Adds scaled ingredients to the primary shopping list (with pantry subtraction preview).

### Tier 2 — Quick Personal Feedback (Compact Header Icon/Pill Controls)

- **Favorit (`♥`):** Instant optimistic toggle (`POST/DELETE /api/v1/recipes/{id}/favorite`).
- **Bewertung (`1–5 ★`):** Inline star rating + personal cook note (`PUT /api/v1/recipes/{id}/rating`).
- **Portions-Stepper (`- / + Portionen`):** Live-scales all ingredient amounts and per-serving/total view.

### Tier 3 — Secondary & Management Menu (`⋯` Overflow Menu)

- **Heute gekocht (`POST /api/v1/recipes/{id}/cooked`):** Logs cook history, deducts pantry items, and offers 1-tap diary logging.
- **Zu Kochbuch hinzufügen:** Assigns the recipe to one or more manual cookbooks.
- **Teilen:** Native share sheet (`@capacitor/share` / Web Share API) or public read-only share link.
- **Bearbeiten (`/rezepte/[id]/bearbeiten`)** & **Duplizieren / Als Variante speichern**.
- **KI-Bild generieren / Nährwerte neu berechnen**.
- **Rezept löschen** (styled in `--color-danger`, with confirmation dialog).
