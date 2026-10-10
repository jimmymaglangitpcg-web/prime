# PRIME — Testing

| | |
|---|---|
| Status | §2 (calculation matrix) written in Phase 14 step H3, 2026-10-09; §3 (performance) in H4; §4 (end-to-end suite, CI) and §5 (accessibility) in H5. The rest comes with H8 |
| Sources | CLAUDE.md §74, §75, §102 Rule 3, §107; `docs/analysis/production-hardening.md` §4.6–§4.7; `docs/BUSINESS-RULES.md` |

## 1. Running the tests

```text
dotnet build Prime.slnx
dotnet test Prime.slnx                 # Domain, Application and Integration tests
cd frontend/prime-web && npm run build && npm run lint   # tsc -b + vite build; oxlint
```

The integration tests use the local PostgreSQL 16 + PostGIS development database (the API's Development
configuration, `appsettings.Development.json`, which `WebApplicationFactory` loads). Each test runs in a transaction that is rolled back, apart from the
fixed `TEST_` reference set noted in `production-hardening.md` §9 (H1). All values in tests are DEMO data.

On 2026-10-09 (end of H3): 836 tests pass (Domain 305, Application 56, Integration 475).

## 2. Calculation matrix (CLAUDE.md §75)

Each cell names the tests that cover it. `D:` = `tests/Prime.Domain.Tests/DomainServices/`, `I:` =
`tests/Prime.IntegrationTests/`. The rules (R1–R23) are in `docs/BUSINESS-RULES.md`.

| §75 cell | Land (R1–R4) | Building (R5–R9) | Machinery (R10–R12) | Assessment (R16–R20) |
|---|---|---|---|---|
| **Zero** | D: `ValuationCalculatorTests.CalculateLand_ZeroArea_IsZeroNotAnError`; D: `CalculationMatrixTests.ZeroInputs_GiveZero_NotAnError`, `Adjustments_OfExactlyMinusOneHundredPercent_GiveZero` | D: `ValuationCalculatorTests.CalculateBuilding_ZeroCompletion_IsZero`; D: `CalculationMatrixTests.ZeroInputs_GiveZero_NotAnError` | D: `CalculationMatrixTests.ZeroInputs_GiveZero_NotAnError` (brand-new and derived) | I: `CalculationMatrixTests.BracketBoundaries_ToTheCentavo` (0 is in the first bracket) |
| **Large amounts** | D: `CalculationMatrixTests.TheLargestArea_TimesALargeRate_IsExact_AndStorable`, `AValueBeyondWhatCanBeStored_IsRefused`, `AValueBeyondDecimal_Overflows_WhichTheApiReportsAsOutOfRange`; I: `CalculationMatrixTests.TheLargestStorableValue_IsStoredExactly_AndOneBeyondIsRefused_WithNothingSaved`, `AnAmountTooLargeForDecimalOrTheColumn_IsA400_NotA500` | Same limit (R18), applied to every row by `ValuationCalculator.ToCentavo` | Same limit (R18) | I: `CalculationMatrixTests.TheLargestStorableValue_…` (its assessed value) |
| **Decimal values** | D: `CalculationMatrixTests.FractionalAreaRateAndAdjustment_KeepFullPrecisionUntilTheCentavo` | D: `CalculationMatrixTests.BuildingDepreciation_OfAFractionalCost_IsTakenFromTheTotalBeforeRounding` | D: `ValuationCalculatorTests.CalculateMachinery_Used_KeepsDecimalPrecision`; D: `MachineryDerivationTests.ReplacementCost_ConvertsAndTrendsTheCostInsuranceFreight_AndAddsTheOtherExpenses` | I: `CalculationMatrixTests.TheAssessedValue_RoundsToTheCentavo_HalfAwayFromZero` |
| **Rounding** | D: `CalculationMatrixTests.Money_RoundsToTheCentavo_HalfAwayFromZero`, `ARowRoundedToTheCentavo_KeepsTheValueBeforeRounding`, `ARowAlreadyInCentavos_IsUnchanged_AndRecordsNoRounding`, `AfterAConfiguredStep_TheValueBeforeTheStepIsTheOneKept`; D: `AdjustmentRulesTests.Rounding_to_the_configured_step`; I: `LandAdjustmentRuleTests.Market_values_round_to_the_configured_step`; I: `CalculationMatrixTests.TheStoredLines_AddUpToTheStoredTotal` | D: `ValuationCalculatorTests.SpreadByArea_RoundsToCentavos_AndTheLastTakesTheRemainder` | D: `CalculationMatrixTests.ARepeatingFraction_RoundsTheSameWayEveryTime` | I: `CalculationMatrixTests.TheAssessedValue_RoundsToTheCentavo_HalfAwayFromZero`, `AValueAFractionOfACentavoOverABracket_IsAssessedAsStored_InTheSameRequestAndLater` |
| **Bracket boundaries** | D: `AdjustmentRulesTests.By_distance_reads_over_the_lower_not_over_the_upper`, `Bands_overlap_when_they_share_a_value` (factor bands) | — | — | I: `CalculationMatrixTests.BracketBoundaries_ToTheCentavo` (one centavo below, at, one above), `AValueAFractionOfACentavoOverABracket_…`; I: `ValueAndAssessTests.LevelBrackets_StayOpenSideBySide_AndMatchOverTheLowerNotOverTheUpper`, `RangesOverlap_ReadsOverTheLowerNotOverTheUpper` |
| **Depreciation limits** | — | D: `BuildingDepreciationTests.TheRemainingValue_CapsTheDepreciation` (at and over the cap), `AnAgeBeyondTheTable_IsRefused`, `Cumulative_TakesTheBandHoldingTheAge` (band edges), `YearlyWithinBand_AddsEachBandsYears`; D: `CalculationMatrixTests.TheLastAgeOfABoundedTable_IsDepreciated_TheNextIsRefused`; I: `BuildingCostValuationTests.CumulativeTable_IsCappedByTheRemainingValue`, `Depreciation_IsCarriedOver_UnlessTheTransactionOrARevisionAllowsANewOne` | D: `ValuationCalculatorTests.CalculateMachinery_Used_NeverBelowSection225Floor`, `…_ExactlyAtFloor_IsNotMarkedAsFloored`, `…_PastEconomicLife_HoldsAtFloorNotZero`, `…_RemainingLifeAboveEconomicLife_IsClamped`; D: `MachineryDerivationTests.Depreciation_IsTheSmallerOfTheLifeRatioAndFivePercentAYear` (equal, capped, under), `TheMinimum_HoldsWhileInOperation_AndDepreciationNeverExceedsTheCost`, `EnteredMethod_HasNoMinimum_ForMachineryNotInOperation` | — |
| **Adjustments** | D: `ValuationCalculatorTests.CalculateLandStrip_AdjustmentsAdd_AndEachIsRecorded`, `CalculateLandStrip_LegacyLocationFactor_AppliesAfterAdjustments_ThenScheduleLimits`, `CalculateLand_AboveScheduleMaximum_ClampsToMaximum`, `CalculateLand_BelowScheduleMinimum_ClampsToMinimum`; D: `AdjustmentRulesTests` (each rule kind); I: `LandAdjustmentRuleTests.Each_rule_takes_its_percentage_from_the_land`, `A_factor_that_cannot_apply_stops_the_valuation_with_the_reason`; I: `CalculationMatrixTests.Adjustments_DeductingMoreThanTheWholeValue_AreRefused` | Extra items: I: `BuildingCostValuationTests.AnExtraItemOutsideTheSmv_IsValuedByItsIndependentAppraisal` | Exchange rate and price index: D: `MachineryDerivationTests.ReplacementCost_…` | — |
| **Multiple classifications on one unit** | I: `AssessmentLinesTests.TwoUses_GiveTwoLines_EachAtItsOwnLevel_WithTheLinesOwnMarketValueAsBracket` | I: `BuildingCostValuationTests.DepreciatedBuilding_GivesTheFaasFigures_AndIsAssessed` (use portions) | I: `MachineryAppraisalTests.TwoMachines_OneFaas_OneLineEach_AssessedUnderTheirOwnUse` | I: `AssessmentLinesTests.UnitBracketBasis_LooksUpEveryLinesLevelWithTheUnitsTotal`, `LineWithNoLevel_IsRefused_NamingIt` |
| **Historical rules** | I: `ValuationDateTests.A_unit_is_valued_under_the_rules_in_force_on_the_valuation_date`, `A_general_revision_values_and_assesses_as_of_its_effectivity`; I: `BackTaxTests.FivePeriods_EachValuedUnderItsOwnSmv_AndAssessedAtItsLevel` | I: `BuildingCostValuationTests.AssessingUnderAnotherTransaction_IsRefused` | D: `MachineryDerivationTests` (indices of the acquisition and valuation years) | I: `EffectivityRuleTests` (derived effectivity, approval date); D: `EffectivityRulesTests`, `BackTaxPeriodsTests` |
| **Deterministic** | D: `CalculationMatrixTests.ARepeatingFraction_RoundsTheSameWayEveryTime`; I: `CalculationMatrixTests.AValueAFractionOfACentavoOverABracket_…` (the same valuation assessed in the computing request and later gives the same result); I: `ValueAndAssessTests.Preview_GivesWhatCreateSaves_WithoutSaving` | | | |

**What the matrix found (H3).** Rows were rounded to the centavo only by the database on save. A general revision
values and assesses a unit in one request, and so assessed the unrounded value, while a manual assessment of the
same valuation read the stored, rounded one. Stored rows did not always add up to the stored total. The fix is
`BUSINESS-RULES.md` R16. A value too large for `numeric(18,2)` or for `decimal`, and adjustments deducting more than
the whole value, gave a server error. They are now refused with a clear message (R18, R3).

Not in the matrix: the frozen treasury calculations (billing and collection, CLAUDE.md §0) keep their own tests and
are not reviewed here.

## 3. Performance at volume (Phase 14 step H4)

Targets (`production-hardening.md` Q7): search and lists under 1 s at the 95th percentile, Property Profile under
1.5 s, tax map view under 2 s, province-wide general revision within one night as a background job.

Set: the DEMO volume database `prime_volume` (`generate-volume`; 250,000 land properties, 400,000 RPUs, 16 DEMO
municipalities, about 1.2 million audit rows), PostgreSQL 16 + PostGIS on the development machine, API published in
Release on the same machine. The province's real counts are DOMAIN VERIFICATION REQUIRED.

```text
psql -h localhost -U prime -d prime_volume -At -f tests/perf/samples.sql > samples.json
node tests/perf/measure.mjs http://localhost:<port> samples.json [case filter]
```

Measured 2026-10-10 on the posted set, after the province-wide Value run (400,000 units valued), one municipality's
19,547 assessments submitted for review and one barangay's approved and posted with its TDs; the audit log then held
5.7 million rows (milliseconds; 20 calls per case; before the H4 fixes in brackets where changed):

| Case | p50 | p95 | max | Target |
|---|---:|---:|---:|---|
| Property search: PIN fragment | 101 | 219 (1,007) | 280 | < 1 s |
| Property search: exact PIN | 63 | 85 (1,229) | 91 | < 1 s |
| Property search: no match | 17 | 27 (1,481) | 28 | < 1 s |
| Property list: barangay | 29 | 34 | 38 | < 1 s |
| Property list: municipality, page 200 | 69 | 225 | 291 | < 1 s |
| Owner search: surname | 45 | 66 (558) | 160 | < 1 s |
| Property Profile (all its calls, in parallel) | 154 | 251 | 787 | < 1.5 s |
| Tax map: barangay zoom | 51 | 86 | 105 | < 2 s |
| Tax map: municipality zoom | 266 | 332 | 335 | < 2 s |
| Tax map: click a parcel | 16 | 44 | 58 | < 1 s |
| Audit trail: page 1 | 16 | 34 (722) | 43 | < 1 s |
| Audit trail: one table, page 100 | 17 | 33 (over 30 s) | 98 | < 1 s |
| Audit trail: a property's history | 38 | 53 (715) | 59 | < 1 s |
| Audit trail: one record | 16 | 19 | 20 | < 1 s |
| SMV schedule rows (one per barangay and class) | 208 | 236 | 278 | < 1 s |
| Approval queue: province, 19,547 pending | 32 | 45 (1,525) | 47 | < 1 s |
| Register: TMCR of a section, issued | 1,374 | 2,350 | 2,656 | none |
| Register: TMCR of a barangay, issued | 1,039 | 1,178 | 1,307 | none |
| Register: assessment roll of a barangay, issued | 1,844 | 2,099 | 2,109 | none (140 s before) |

A register run builds its rows and freezes the printed register, so it is timed but has no list target.

General revision, province-wide (400,000 units): Compile in 5.5 minutes; Value at 760 to 1,130 units a minute, about
9 hours in all on this machine (the 2026-10-09/10 run stopped overnight and resumed). Batch actions for one barangay
of 1,028 units: submit 13 s, approve 48 s, post 38 s, submit TDs 18 s, approve TDs 355 s (each approval freezes the
printed TD and FAAS). Everything is measured again on the hosted stack (H6), where every statement crosses the network.

## 4. Browser end-to-end suite and CI (Phase 14 step H5)

Playwright (`frontend/prime-web/e2e/`, Chromium) drives the Development build against the local API with its sign-in
bypass, acting as the DEMO users of the API's development configuration (the `X-Prime-Dev-Act-As` key the build keeps in
`localStorage`). It works on the DEMO E2E set: a DEMO province with two municipalities of one barangay each, one land
classification and actual use, a sole-owner ownership type, and an approved SMV (1,000 per sqm) and assessment level
(20 %). The set is written by an idempotent command, refused outside Development or for a database not on this machine:

```text
dotnet run --project src/Prime.WebApi --launch-profile http -- seed-e2e   # once; then (re)start the API
cd frontend/prime-web
npx playwright test                 # reuses an API on :5221 and Vite on :5173 if running, else starts both
npx playwright test access          # one file;  AXE_REPORT=1 prints every axe finding
```

The tests share one database and act as maker and checker in turn, so they run one at a time (about 3.5 minutes).
Records they make are DEMO and tagged per run; history cannot be deleted (H1), so they accumulate in `prime_dev` like the
integration tests' fixed set. `tsc -b` type-checks the suite (`tsconfig.e2e.json`).

| File | What it proves |
|---|---|
| `assessment-flow.spec.ts` | CLAUDE.md §74 on screen: register a property, its owner (a new taxpayer from the party dialog), parcel, land RPU and land; value (500,000) and assess (100,000); the maker is offered no decision; a second user approves and posts; the TD declaring the assessment is prepared, submitted and approved; the FAAS is issued and shows the PIN and value; the NOA is generated and issued; the taxable assessment roll and the Record of Assessment are run and issued and list the TD and owner |
| `access.spec.ts` | A viewer's write is refused (403 `PERMISSION_DENIED`, and on screen with the reason) and the menu hides what the role cannot open; the creator of an assessment cannot approve it although their role could; a municipal appraiser finds a property of their municipality but not one of another, which opens as "not found" and refuses writes (404); a new applicant (`applicant-<tag>`, a fresh identity per run) sends a sign-up request and an administrator approves it, after which PRIME opens for them |
| `accessibility.spec.ts` | axe (WCAG 2.1 A and AA tags) on 11 main screens; serious and critical findings fail (§5) |
| `keyboard.spec.ts` | A property registered with Tab, typing and Enter only, with a visible focus indicator at each step |

CI (`.github/workflows/ci.yml`, GitHub Actions) on every push to `master` and pull request: backend restore, build,
migrations applied to a PostGIS 16 service container, all .NET tests, `dotnet list package --vulnerable`; frontend
`npm ci`, `tsc -b` + `vite build`, oxlint, `npm audit --omit=dev --audit-level=high`; a gitleaks secret scan over the
history. The end-to-end job runs on demand (Actions → CI → Run workflow → e2e) and before a release: it migrates a fresh
database, and `playwright.config.ts` runs `seed-e2e`, starts the API and Vite, and runs the suite. CI holds only DEMO data
(CLAUDE.md §118).

A database built from migrations alone holds no reference data (it comes from content packs). Checked 2026-10-09 on a
fresh local database (`prime_ci`, PostGIS created by the `postgres` superuser): 22 integration tests failed, 21 because they
take the first municipality, barangay, classification or `LAND` property type they find, or need the DEMO municipal
users' office, and one because two payments posted in one transaction came back in either order (the test now ignores
their order; billing is frozen). After `seed-e2e` all 477 pass. CI therefore runs `seed-e2e` after the migrations. On a
fresh database run it before the integration tests:

```text
ConnectionStrings__PrimeDb="Host=localhost;Port=5432;Database=prime_ci;Username=prime;Password=..."   dotnet run --project src/Prime.WebApi --no-launch-profile -- seed-e2e      # with ASPNETCORE_ENVIRONMENT=Development
```

## 5. Accessibility (WCAG 2.1 AA; production-hardening.md Q10)

Automated, 2026-10-09: axe on Dashboard, Property search, Register property, Property profile, Taxpayer search, Awaiting
my approval, Registers, Tax map (map canvas excluded), General revision, Offices and Audit trail. First run: one kind of
finding on 10 of the 11 screens, `color-contrast` (serious): Ant Design's secondary text (45 % black, 3.4:1), select
placeholders (25 % black), preset tag text (the colour's 7th shade on its lightest tint, about 3:1 for green) and links
in the default link blue. Fixed in the theme (`main.tsx`: secondary, tertiary, description and placeholder text, link
colour) and in `index.css` (darker text shades for the preset tag colours). Now: no violations of any impact on the 11
screens.

Keyboard: automated pass of property registration (`keyboard.spec.ts`). A person's manual keyboard pass of the main
workflows (search, profile tabs, the dialogs of the §74 flow, approvals, registers) is still to be done and recorded here.
