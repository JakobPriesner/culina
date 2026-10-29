# Onboarding & Empty-State Design

Specification for first-run household onboarding, starter recipes, and contextual empty states (`src/frontend/src/lib/components/onboarding/` and `src/backend/Culina.Infrastructure/Services/StarterRecipeSeeder.cs`).

---

## 1. Design Principles

- **Zero Mandatory Wizards:** New users land directly on the functional app after registration or passkey setup — never behind a multi-screen blocking questionnaire.
- **Immediate Value via Starter Pack:** Empty households can seed a curated pack of classic German/Mediterranean starter recipes with one click (`POST /api/v1/recipes/seed-starters`) or import straight from a URL, photo, or third-party archive (`.paprikarecipes`, Mealie, Tandoor, Nextcloud).
- **Progressive Feature Discovery:** Advanced features (household invites, AI keys, nutrition goals, pantry staples) surface inline at the moment of need rather than up front.

---

## 2. First-Run Checklist & Starter Seeding

When a household has `0` recipes (`recipes.length === 0`), `Heute` (`/`) and `Sammlung` (`/rezepte`) render a warm welcome card offering three direct paths:

1. **Starter-Rezepte laden:** Seeds 5 complete showcase recipes (with ingredients, step timers, tags, and BLS nutrition) so search, scaling, cooking mode, `Woche`, and `Einkaufsliste` can be tested immediately.
2. **Erstes Rezept importieren (`/rezepte/neu`):** Opens URL, photo/PDF, or text intake.
3. **Aus anderer App umziehen:** Links directly to Paprika / Mealie / Tandoor / Culina ZIP import.

---

## 3. Contextual Empty States Across Surfaces

Every empty view provides a 1-sentence German explanation and a single primary CTA:

| Surface                        | Empty Condition               | Primary CTA                                                                                                   |
| ------------------------------ | ----------------------------- | ------------------------------------------------------------------------------------------------------------- |
| **Heute (`/`)**                | No meal planned today         | `Woche planen` (`/woche`) or `Vorschlag wählen`                                                               |
| **Sammlung (`/rezepte`)**      | Filter/query returns `0` hits | One-click filter relaxation pills (`RecoveryLadder`)                                                          |
| **Woche (`/woche`)**           | Empty week grid               | `Vorschläge anzeigen` (opens contextual picker)                                                               |
| **Einkauf (`/einkaufsliste`)** | No unchecked items            | `Aus Wochenplan füllen` + inline quick-add input                                                              |
| **Küche (`/kueche`)**          | No pantry items               | `Grundvorrat einrichten` (1-tap staple checklist: Salz, Pfeffer, Olivenöl, Mehl, Zucker, Zwiebeln, Knoblauch) |
| **Tagebuch (`/tagebuch`)**     | No food logged today          | Planned meals from `Woche` (`Jetzt loggen`) + `Lebensmittel hinzufügen`                                       |
