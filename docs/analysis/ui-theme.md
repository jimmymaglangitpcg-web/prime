# UI theme: logo, navigation, colour and type (design)

| | |
|---|---|
| Step | UI theme, between Phase 11 R2 and R3 (user decision, 2026-10-10; roadmap "Phase 11") |
| Status | Done 2026-10-10 (T1–T5; decisions §7.1, log §8) |
| Sources | CLAUDE.md §2, §82–§85; the theme proposal the user reviewed on 2026-10-10 (private design canvas); docs/analysis/production-hardening.md Q10 (WCAG 2.1 AA) |
| Depends on | The dashboard (R2), the sign-in screens (Phase 12), the e2e accessibility and keyboard checks (H5) |

## 1. Scope

PRIME looks like an unstyled Ant Design application: a plain "PRIME" word in the sidebar, 26 menu entries in one flat
list, Ant Design's default blue and the system font. On 2026-10-10 the user reviewed a proposal (logo, grouped sidebar,
colours, type) and said they liked it. This step builds it. It changes how screens look and how the menu is
organised. It does not change what any screen does, what a user may do, or any API.

## 2. What exists

| Area | Now | Where |
|---|---|---|
| Logo | The word "PRIME" (or "P" collapsed) in white text | `components/AppShell.tsx` |
| Menu | 26 entries in one flat list, each shown only when the user's roles allow its permission | `navItems` in `AppShell.tsx` |
| Header | The full system name as the page title, the office tag, the dev user picker, sign out | `AppShell.tsx` |
| Colours | Ant Design's dark sidebar (#001529), primary #1d4ed8, darker text greys for contrast | `main.tsx` theme tokens |
| Status tags | Darker text shades on Ant Design's preset tag colours (4.5:1) | `index.css` |
| Type | System font stack | `index.css` |
| Sign-in screens | A centred card with "PRIME" in blue and the full name below | `pages/auth/AuthLayout.tsx` |
| Treasury screens | "Collection" and "Collection Setup" in the menu for `treasury.legacy` (frozen, CLAUDE.md §0) | `AppShell.tsx` |

## 3. Gaps

| # | Gap |
|---|---|
| U1 | No product mark; CLAUDE.md §85 asks for PRIME branding with no LGU seal until one is supplied |
| U2 | 26 menu entries in one list: a user scrolls the sidebar, and related screens (SMV preparation, testing, impact study) are not grouped |
| U3 | The header spends its width on the full system name; the global search of §56 has no place in the shell |
| U4 | Default Ant Design colours and the system font; amounts and PINs are not set in tabular figures |
| U5 | The frozen treasury screens still sit in the menu beside the assessor's work |

## 4. Proposal

### 4.1 Logo (U1)

A product mark, not a seal: a tax-map parcel outline with a dashed subdivision line and a small amber survey-monument
dot, on a rounded navy tile, beside the word "PRIME". It is an inline SVG component (`components/PrimeLogo.tsx`),
used in the sidebar (tile only when collapsed), on the sign-in screens and as the browser favicon. An office's own
logo, when one is configured (CLAUDE.md §85), goes on letterheads and forms as now, not in the application's mark.

### 4.2 Sidebar: 26 entries into 7 (U2, U5)

| Entry | Holds (each still shown only with its permission) |
|---|---|
| Dashboard | — |
| Approvals | Awaiting my approval, with the count of records waiting |
| Registry | Properties; Owners & taxpayers; Sworn statements; Exemptions |
| Tax map | — |
| Valuation | Market data; SMV preparation; SMV testing; Tax impact study; General revision |
| Records | Registers; Submissions; Reports |
| Administration | People: Offices, Role permissions, Sign-up requests (count) · Configuration: Property identification, Valuation rules, Forms & numbering, Content packs · Oversight: Audit trail, System health |

- A group with one permitted entry left shows as that entry. A group with none is hidden.
- The open group follows the current page; the group of a deep page (a property, a revision) is open with its entry
  selected, as the longest-prefix match does now.
- "Collection" and "Collection Setup" leave the menu. Their pages stay reachable by URL for `treasury.legacy` users
  until the treasury code is removed (CLAUDE.md §0, §105).
- Labels change where noted: "Taxpayers" becomes "Owners & taxpayers"; "Awaiting my approval" becomes "Approvals" in
  the menu (the page keeps its title).

### 4.3 Header (U3)

- The full system name leaves the header (it stays on the sign-in screens and in the page title of the browser tab).
- A global search box takes its place: it opens the property search with the typed text, which searches PIN, lot,
  title, survey and tax-map numbers as now (CLAUDE.md §56). The property search page learns to read its query from the
  address, so the search can be bookmarked. Searching TD numbers and owners from the same box is a later step (Q4).
- The signed-in user (name, office, roles) and sign out move to the foot of the sidebar. The office tag stays in the
  header at desktop width.

### 4.4 Colour (U4)

| Token | Value | Use |
|---|---|---|
| Navy | #10263D | Sidebar, logo tile, sign-in panel |
| Primary | #1F5C99 | Buttons, links, selected menu entry, chart bars (6.9:1 on white) |
| Amber | #F2A33A | Attention only: counts waiting, the logo's monument dot. Never text on white (about 2:1) |
| Ink | #17212B | Body text |
| Slate | #475563 | Secondary text (7.6:1 on white, 7.1:1 on mist) |
| Mist | #F4F6F8 | Page background behind cards |

- Set as Ant Design theme tokens in `main.tsx` (one place), plus the sidebar's Menu tokens. The status tag shades of
  `index.css` stay; each is re-checked against the new background.
- Every text pairing is checked for WCAG 2.1 AA (4.5:1) and the dashboard's bar colour against the chart validator
  before it ships; the e2e axe checks must stay free of serious and critical findings.
- Light theme only, as now (a dark theme is not requested; Q6).

### 4.5 Type (U4)

- IBM Plex Sans for the interface and IBM Plex Mono for PINs, TD numbers and other document numbers (SIL Open Font
  Licence). Amounts in tabular figures, so columns of values line up.
- The fonts are **bundled with the application** (npm `@fontsource/ibm-plex-sans` and `@fontsource/ibm-plex-mono`, only
  the weights used), not loaded from Google: no request to a third party from an office's browser, nothing to add to
  the security headers, and the fonts work where the office's internet is slow (Q5).

### 4.6 Sign-in screens

A split page: on the left a navy panel with the logo, the full system name, the office name from configuration and a
faint tax-map parcel pattern; on the right the existing sign-in, create account and forgot password forms as segmented
tabs. The access request, awaiting approval and MFA screens use the same layout. The MFA code becomes six boxes, one
digit each, accepting a pasted code (now one input). At phone width the panel shrinks to a band above the form.

### 4.7 Not changed

Screens' content and behaviour, permissions, the API, printed forms and registers (their layouts are content), the
map's layer colours, and the frozen treasury pages' look.

## 5. Delivery steps

| Step | Content |
|---|---|
| T1 | Tokens and type: colour tokens, bundled Plex fonts, tabular figures, mono for document numbers; contrast re-checked |
| T2 | Logo component and favicon; grouped sidebar with counts; treasury entries out of the menu; user at the sidebar foot |
| T3 | Header search; property search reads its query from the address |
| T4 | Sign-in screens: split layout, six-box MFA code |
| T5 | Browser pass: each role's menu, keyboard through the grouped menu, axe on the e2e screens, phone width; e2e tests that name menu entries updated |

Each step is built, the frontend production build and lint run, and the screens checked in a browser before the next.

## 6. Exit criteria

1. Every screen reachable before is reachable from the grouped menu (or by URL for the treasury pages), with the same
   permissions.
2. The e2e suite passes, including axe with no serious or critical findings and the keyboard-only registration.
3. No sideways page scroll at phone width; the collapsed sidebar shows the logo tile.
4. No font or other asset is loaded from a third-party host.

## 7. Review questions

| # | Question | Recommendation |
|---|---|---|
| Q1 | Are the colours of §4.4 final? | Yes, as reviewed: navy, primary blue, amber for attention only |
| Q2 | Is the logo of §4.1 final? | Yes, as a product mark; replaced or joined by an LGU logo only on documents |
| Q3 | Is the grouping of §4.2 final, with the renamed entries? | Yes |
| Q4 | Header search: property search only now, or also TD numbers and owners? | Property search now (it exists); a combined search of §56 (TD, RPU, owner, TIN) later, as its own small step with an API |
| Q5 | Fonts bundled with the application or loaded from Google Fonts? | Bundled (§4.5) |
| Q6 | Dark theme? | No; light only, as now |
| Q7 | Treasury entries out of the menu, pages kept by URL? | Yes, until the treasury code's removal is planned (§0, §105) |
| Q8 | Six-box MFA code and split sign-in page now (T4), or later? | Now, with this step |

### 7.1 Decisions (2026-10-10)

The user accepted every recommendation:
- Q1–Q3: the colours, the logo and the grouping with its renamed entries are final.
- Q4: the header search opens the property search; a combined search of TD, RPU, owner and TIN is a later step.
- Q5: the fonts are bundled with the application.
- Q6: light theme only.
- Q7: the treasury entries leave the menu; their pages stay reachable by URL.
- Q8: the split sign-in page and the six-box MFA code are built in this step (T4).

## 8. Implementation log

### T1 — tokens and type (2026-10-10)

- `src/theme.ts` holds the palette and the Ant Design theme (tokens, sidebar and menu colours); `main.tsx` uses it.
  Besides §4.4's colours, error, warning and success text get darker shades than Ant Design's (red 3.3:1, gold about
  2:1): `#C0362C` (5.5:1), `#8A5300` (6.3:1), `#237804` (5.6:1). The axe check found the red on the dashboard's "failed"
  count once the revision card had loaded.
- IBM Plex Sans (400, 500, 600) and Plex Mono (400, 500), Latin subset, bundled from `@fontsource/ibm-plex-sans` and
  `@fontsource/ibm-plex-mono` 5.3.0 (OFL-1.1): about 120 kB of woff2. No request leaves the application's origin.
- Tabular figures in tables, statistics and description lists; `DocNumber` sets PINs and TD numbers in Plex Mono on the
  dashboard, property search, property profile, its units' TDs and the approvals inbox.
- The dashboard's charts and revision card show a loading state instead of "No FAAS in force" while loading.

Verified: production build and lint clean; the 17 e2e tests pass, axe without serious or critical findings on the 11
screens; fonts load from the bundle (no third-party requests).

### T2 — logo and grouped sidebar (2026-10-10)

- `PrimeLogo` / `PrimeMark` (inline SVG, `components/PrimeLogo.tsx`); the same drawing is `public/favicon.svg`; the browser
  tab reads "PRIME".
- The sidebar is the seven entries of §4.2 (`navEntries` in `AppShell.tsx`), filtered by permission; a group with one
  permitted screen shows as that screen. The current screen's group opens when the user arrives on it. Approvals shows
  the count of records waiting (amber badge, navy figure). The treasury screens are out of the menu, reachable by URL.
- The sidebar stays in view while the page scrolls; its foot shows the signed-in user (name, roles; office and roles in
  the tooltip) and sign out. At phone width, where the sidebar is hidden, sign out stays in the header.
- Found on the way: Ant Design 6.6's button loading icon leaves by a CSS transition with no fallback timer. When a request
  settles very quickly the transition never ends and the invisible icon stays, so the button's accessible name reads
  "loading Approve" (the e2e maker-checker test failed on it, with the committed frontend too). `index.css` hides the
  leaving icon at once.
- e2e: the access test opens Administration before checking which entries a view-only user lacks, with the new labels.

Verified: production build and lint clean; the 17 e2e tests pass (axe, keyboard, access, the §74 flow).

### T3 — header search (2026-10-10)

- The header's search box (users with `property.view`) opens `/properties?q=…`; the property search reads its text from
  the address, so a search can be bookmarked, and a new search starts at the first page. The full system name left the
  header.
- Found at phone width: the property search's own box was 600 px inside a wrapping row and pushed the page sideways; it
  now shrinks with the screen. Nine tables on the general revision, market data and property screens had no scroll area
  of their own and widened the page; each now scrolls in its own box.
- e2e `shell.spec.ts`: a PIN typed in the header finds the property, the address carries it, the Registry group is open,
  and the same address opened directly gives the same result.

### T4 — sign-in screens (2026-10-10)

- `AuthLayout` is the split page of §4.6: a navy panel with the mark, the full name, the office and LGU from
  `VITE_LGU_OFFICE` / `VITE_LGU_NAME` (none by default) and a faint drawn parcel pattern; the form on the right; a band
  above the form at phone width. It serves sign in, the access request, awaiting approval, the MFA step, the password
  reset and the gate's notices.
- Sign in, new account and forgot password are a segmented switch; the heading follows the choice.
- The MFA code is six boxes (Ant Design's `Input.OTP`, digits only, one-time-code autofill, a pasted code fills every
  box, Enter verifies), in a group labelled "Authenticator code, 6 digits".

### T5 — checks (2026-10-10)

Verified: production build and lint clean; the 18 e2e tests pass (axe without serious or critical findings on the 11
screens, keyboard-only registration, access, maker-checker, the §74 flow, the new shell test); axe without findings on
the sign-in and access-request pages; no sideways scroll at phone width on the sign-in page and on dashboard, approvals,
properties, owners, registers, reports and general revision; the grouped menu works by keyboard (Enter opens a group,
arrows move, Enter opens the screen).

Not verified in a browser: the MFA step itself, which needs a real Supabase session with a role that requires a second
factor. It is left for the user's local testing (H5).
