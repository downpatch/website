# Downpatch accessibility audit

Date: 3 September 2026  
Target: WCAG 2.2 Level AA  
Compared revisions: pre-PR parent `f889715` and merged PR #4 `1e7dcd3`

## Executive summary

The pre-PR application had the preferred black background and white text, but it was not fully WCAG conformant. PR #4 changed 18 files (`+2,127/-457`), introduced a blue, glow-heavy visual treatment and added useful guide features, while retaining several existing accessibility defects and introducing new ones in the level card and content renderer.

The remediation keeps the useful guide metadata and Markdown features, restores a restrained black/white presentation, removes decorative glows and third-party font/icon styles, and repairs the application shell and guide templates against WCAG 2.2 AA. Automated source and rendered-DOM checks now pass for all 162 guide routes.

This is an engineering audit, not a legal certification. Third-party video captions and the editorial quality of legacy guide text alternatives still require human review by the content maintainers.

## Before and after

| Area | Before PR #4 | Merged PR #4 | Remediated result |
| --- | --- | --- | --- |
| Visual design | Predominantly black and white, with some low-opacity UI | Blue-tinted surfaces, gradients, glows, shadows, blur and remote fonts/icons | Black background, white text, plain surfaces and borders; no gradients, glow, shadow, blur or decorative filters in active app CSS |
| Landmarks | Site brand was an H1 and guide pages nested a second `main` inside the layout `main` | Defects remained | One `main` and one page H1; brand is no longer a heading; guide document is an `article` |
| Mobile navigation | Large guide navigation remained in the page flow on small screens | Sticky/full-height navigation could block access to content and create horizontal scrolling | Native collapsed `details` navigation and table of contents; content is first-class and immediately reachable |
| Guide cards/search | Icon-led controls and links with overridden `listitem` roles | Defects remained | Labelled search field and text buttons; semantic `ul`/`li`; cards retain their native link role |
| Level information | Not present | Added color accents and difficulty controls without reliable selected-state semantics | Semantic headings and definition lists; text labels do not rely on color; toggle buttons expose explicit `aria-pressed=true/false`; updates are announced |
| Guide content | Existing heading, image and animation quality varied | Added strategy/YouTube rendering, but irregular blocks and some video formats leaked raw markup | Heading levels and sequences normalized; strategy metadata is a definition list; all image elements have `alt`; all GIFs can be hidden; tables are keyboard-scrollable; all supported YouTube forms produce titled iframes |
| Navigation aids | Skip targets could resolve to the site root because of the base URL; 404 had no H1 | Defects remained | Route-correct skip links, visible focus, current-page state, page titles and an H1 on not-found/error routes |
| External links | Floating controls could overlay content and their labels disappeared on mobile | Defects remained | Static footer links with persistent names; every new-tab link announces that behavior |

## Findings remediated

- **1.1.1 Non-text Content:** decorative card/hero images have empty alternatives; rendered guide images always receive an `alt` attribute.
- **1.3.1 and 1.3.2 Info, Relationships and Sequence:** corrected landmarks, headings, lists, breadcrumbs, definition lists and document order. Heading jumps are normalized without adding a second page H1.
- **1.4.3 and 1.4.11 Contrast:** replaced opacity-based text and borders with explicit high-contrast colors. Key measured ratios are:
  - white text on black: `21.00:1`
  - muted text on black: `14.25:1`
  - links on black: `12.86:1`
  - visited links on black: `11.88:1`
  - standard border on the raised surface: `3.91:1`
  - yellow focus indicator on black: `15.59:1`
- **1.4.10 Reflow:** the home, guide index and long guide page reflow at 320 CSS pixels with no document-level horizontal scrolling.
- **2.1.1 Keyboard:** search, disclosures, guide links, tables and difficulty controls are keyboard operable.
- **2.2.2 Pause, Stop, Hide:** all nine animated GIF demonstrations start inside closed native disclosures so readers can choose whether to show them.
- **2.4.1, 2.4.2 and 2.4.6 Navigation and Labels:** route-correct skip links, descriptive page titles, labelled regions and labelled controls.
- **2.4.3, 2.4.7 and 2.4.11 Focus:** logical focus order, a high-contrast three-pixel focus indicator and no floating controls obscuring focus.
- **2.5.8 Target Size:** primary controls use a minimum 44-pixel target; rendered checks found no non-inline target below 24 by 24 pixels.
- **3.2.4 Consistent Identification:** repeated site and document actions now use stable visible text.
- **4.1.2 and 4.1.3 Name, Role, Value and Status Messages:** native link roles are preserved, difficulty state uses valid ARIA values, and search counts/goal changes use live regions.

## Validation performed

- `dotnet build`: succeeded with 0 warnings and 0 errors.
- Rendered all 162 Markdown guide routes: 162 succeeded and 0 failed.
- Across those routes: one H1, one `main`, no nested `main`, no duplicate IDs, no skipped heading levels, no images without an `alt` attribute, no untitled iframes, no raw strategy/YouTube tags and no unwrapped tables.
- Renderer totals checked: 12 strategy cards, 40 titled YouTube iframes, 9 keyboard-scrollable table regions and 9 user-controlled animated images.
- Browser checks at 320, 390 and 1280 CSS pixels: no document overflow, unnamed controls, missing image alternatives, invalid pressed states or detected text-contrast failures.
- Keyboard checks: visible focus indicator, guide search/filter, mobile guide navigation, mobile on-page navigation and difficulty selection/live result update.
- Static scan of active application CSS: no gradient, box shadow, text shadow, backdrop filter, drop shadow or `rgba()` opacity styling. The unused Bootstrap stylesheet and remote font/icon styles are no longer loaded.
- High-contrast, reduced-motion, increased-contrast and print rules are present.

## Human/editorial checks still required

Code can ensure that media is exposed correctly, but it cannot prove that the content itself is equivalent:

1. Confirm that each of the 40 third-party YouTube tutorials has accurate captions, and provide a transcript where the video conveys instructions not available in nearby text (WCAG 1.2.2 and 1.2.3).
2. Review legacy image alternatives that are currently filenames, such as `GenRamp.PNG` or `reunionjump.gif`, and replace them in the content repository with descriptions of the information shown (WCAG 1.1.1).
3. Confirm that the surrounding text for the silent local Barrier Jump video communicates every instructional visual detail, or add a text description (WCAG 1.2.1).
4. Include keyboard-only and screen-reader spot checks in release QA, because assistive-technology behavior cannot be completely established by static or DOM automation.

Until those editorial checks are complete, the accurate statement is: **the application shell and rendering templates have been remediated toward WCAG 2.2 AA; full end-to-end conformance of every guide depends on its maintained media and content.**
