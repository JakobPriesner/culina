# Collection navigation: 21 approaches and a decision

## Goal

Make Recipes and Cookbooks easy to find without making the header look like two
competing navigation bars. Keep recipe creation directly accessible. Support more
library features without reserving a large amount of space for navigation.

This is a design assessment, not user-research evidence or a claim about what an
Apple employee would decide. The criteria are discoverability, number of actions,
content space, hierarchy, room for growth, and phone usability.

Apple's [sidebar guidance](https://developer.apple.com/design/human-interface-guidelines/sidebars)
supports labelled navigation on the leading side and more compact navigation when
space is limited. Its [materials guidance](https://developer.apple.com/design/human-interface-guidelines/materials)
separates controls from content; glass is a supporting material, not a substitute
for a clear hierarchy.

## Twenty alternatives, plus the requested slim sidebar

| # | Approach | Assessment from a user's perspective |
| --- | --- | --- |
| 1 | Header segmented switch | One click, but duplicates the main navigation and reads like a view setting. Rejected in the current UI. |
| 2 | Large tabs beside the page title | Easy to spot at first; either disappear on scroll or recreate the second header row. |
| 3 | Underlined sticky tabs above the collection | Clear selection with little decoration; still takes a horizontal band over content. |
| 4 | Recipes and Cookbooks as main destinations | Both are always one click away. Strong on phones; additional features quickly compete for header space. **Shortlist.** |
| 5 | Library destination with a dropdown | Groups the collection clearly, but hides Cookbooks behind an extra interaction. |
| 6 | Collection picker in the page title | Very quiet and space-efficient. Current location is clear, but the alternative is less discoverable. **Shortlist.** |
| 7 | Breadcrumb with a sibling menu | Useful for deep navigation, but excessive indirection for two peer destinations. |
| 8 | Library overview with Recipes and Cookbooks sections | Helps discovery and browsing; switching can require returning to an overview and scrolling. |
| 9 | Horizontal cookbook shelf above recipes | Visual and inviting, but hides the destination when there are no cookbooks and mixes navigation with content. |
| 10 | Cookbook drawer opened by a book icon | Preserves recipe space; the icon's meaning and the drawer's contents must be learned. |
| 11 | Command palette for destinations | Efficient for experienced keyboard users; unsuitable as the main navigation for touch or new users. |
| 12 | Search-first library with scope chips | Good for finding known content; forces browsing into a search model. |
| 13 | Unified grid with recipe/cookbook type filters | Avoids separate pages, but filters misrepresent two different kinds of organization. |
| 14 | Recipes and Cookbooks shown side by side | No switching cost, but both collections lose useful width and phones need a different model. |
| 15 | Full-height global sidebar | Very scalable and familiar; too much permanent structure for this app and this request. |
| 16 | Icon-only left rail | Small footprint, but recipe and cookbook symbols are too similar without text. |
| 17 | Collapsible labelled sidebar | Accommodates large libraries; adds a visibility state and another control before either is needed. |
| 18 | Expandable tree under Recipes | Supports deeper hierarchies; makes Cookbooks look subordinate to individual recipes. |
| 19 | Floating local switch at the bottom | Reachable by thumb, but competes with shopping navigation and cooking controls. |
| 20 | Swipe between collection pages | Fast after learning it, but poorly discoverable and unsuitable as the only desktop interaction. |
| 21 | Slim labelled left sidebar, with compact navigation sheet | Visible, stable destinations without a second header row. Uses a small amount of desktop width; adapts to one Library launcher and a vertical list on phones. **Shortlist and winner.** |

## Best three: user-facing tradeoffs

### 1. Slim left sidebar with compact navigation sheet — selected

**Pros:** “I can see where I am and where I can go.” Recipes and Cookbooks remain
one click away. Text labels avoid guessing icons. The header stays focused on
global navigation and creating a recipe. Additional real library destinations can
be added vertically. A small navigation panel is familiar from Settings without
copying its much wider layout.

**Cons:** It uses some recipe-grid width on laptops. Its position changes between
desktop and phone. With only two destinations, a tall or full-height sidebar would
feel oversized.

**Response:** Use a short panel approximately 10rem wide, positioned toward the
left edge. Reserve its own column so it never obscures a card. Keep it sticky on
collection pages, but retain full-width recipe reading and editing. On phones,
open the same labelled destination list from a single Library launcher in the
bottom navigation. This adds one tap to switching but keeps the app bar stable as
the library grows.

### 2. Promote Cookbooks to the main navigation

**Pros:** “Everything is in one place.” There is no secondary navigation to learn;
both destinations stay visible. Four bottom destinations work well on phones.

**Cons:** On desktop, a growing main bar mixes library organization with unrelated
tasks such as shopping and settings. It fixes today's two choices without giving
future library features a clear home.

**Decision:** Reject for the compact layout too: future collection features would
crowd the app bar. Keep one Library launcher there and put its destinations in a
scrollable sheet.

### 3. Page-title collection picker

**Pros:** “The page feels calm, and I get more space for recipes.” It removes
repeated labels from the header and can accommodate additional collections.

**Cons:** “I may not realize there is another collection.” Changing destination
requires opening a menu, which is slower for frequent switching and less visible
to new users. It repeats the discovery problem of the former Actions menu.

**Decision:** Reject as the primary navigation. The user's feedback favors visible,
immediate access over hiding important choices for visual minimalism.

## Implementation contract

- Desktop: a small sticky left collection rail, independent of the top navigation.
- Rail appears on the recipe collection and cookbook collection/detail pages.
- Recipe reading, editing, cooking, planning, and settings retain their page widths.
- Phones/tablets below the desktop breakpoint: Library, Shopping, and Settings
  in the bottom navigation. Library opens the shared collection list in a sheet.
  Future collection features add rows to the list without growing the app bar.
- The complete Culina wordmark returns to the header on phones.
- Direct recipe creation remains in the header on every signed-in route.
- Existing library search/filter state and browser history remain intact.
- Labelled links, one current collection, visible focus, adequate targets, both
  themes/locales, and enlarged text remain part of validation.

## Initial sidebar validation

- Svelte checks: no errors or warnings. Scoped lint and formatting checks passed.
- Design-system, theme, and translation unit checks: 72 passed.
- Library and responsive browser checks: 30 passed, including both locales,
  both themes, narrow widths, and desktop breakpoints.
- Final desktop/mobile library and enlarged-text checks: 10 passed; four
  duplicate mobile matrix cases intentionally skipped by the suite.
- Manually reviewed the desktop sidebar and 320px German phone layout, including
  switching to Cookbooks. Both collection destinations and direct recipe creation
  remain available while scrolling; search state survives collection switching.

## Compact-layout revision

The first implementation promoted Recipes and Cookbooks to separate bottom tabs.
User feedback identified that this repeats the scaling problem on small screens.
The revised adaptation is a collapsible version of the sidebar: one labelled Library
launcher opens a scrollable sheet, with the current collection highlighted.

A horizontal strip would hide later items off-screen; an overflow tab would split
peer destinations into visible and hidden groups. A single collection sheet keeps
the hierarchy consistent and provides room for long labels and future features.
Its cost is an additional tap to change collections. Direct recipe creation stays
outside the sheet so the main action retains immediate access.

Revision validation: Svelte checks reported no errors or warnings; scoped lint,
formatting, and 10 design-system/translation checks passed. Nine browser tests
passed (five duplicate viewport cases intentionally skipped), covering the open
sheet in both themes/languages, search preservation, direct recipe creation,
dismissal/focus return, and transition to the desktop sidebar. The German phone
sheet and navigation to Cookbooks were also reviewed in the running app.

## Workspace hierarchy refinement

The next visual review found that the sidebar card, large editorial heading,
isolated planning link, and pill search read as unrelated controls. The revised
layout aligns the shell and collection on one grid and uses a flat, compact
sidebar. Week planning becomes a navigation destination in both the rail and sheet.
The header names the parent Recipe book area, while page headings identify Recipes
or Cookbooks. Repeated eyebrow text is removed.

Recipe creation remains the green primary action. Cookbook creation uses the same
plus icon and control geometry with neutral emphasis. The search, quick filter,
and collection summary share a toolbar; their text-entry, toggle, and informational
roles remain distinct. A shorter featured recipe introduces the content without
occupying the entire first screen. The compact sheet remains the scalable mobile
adaptation.
