# PRIME — Business rules: appraisal and assessment calculations

| | |
|---|---|
| Status | Started in Phase 14 step H3 (2026-10-09). Covers the calculations PRIME performs; workflow, numbering and form rules stay in their design documents |
| Sources | CLAUDE.md §5–§7, §30–§32, §65, §75; `docs/analysis/valuation-foundation.md`; `docs/analysis/value-and-assess.md`; `docs/analysis/mrpaao-forms-model.md` §8; `docs/analysis/assessment-listing-exemptions.md`; `docs/analysis/current-real-property-regulatory-baseline.md` |
| Tests | Each rule's tests: `docs/TESTING.md` §2 (the §75 calculation matrix) |

This register lists every rule the valuation engine and the assessment calculation apply, as CLAUDE.md §6 asks:
what the rule is, its basis, when and where it applies, and how PRIME represents it. It cites the LAM 2025 and the
law but does not reproduce them (CLAUDE.md §118). No value here is an LGU's: the province's SMV, levels, factors and
tables are configuration, entered under maker-checker.

**Effective date and jurisdiction.** A configured rule (SMV, schedule, building cost, depreciation table, adjustment
factor, assessment level, price index, exemption, transaction type) carries its own effective date and, for an
SMV, the municipalities it covers. A calculation uses the rules in force on its valuation date for the property's
municipality (R13), so "effective date" and "jurisdiction" below say where those come from. The deployment is
province-wide (CLAUDE.md §117). Settings under `Valuation:` and `Assessment:` apply to the whole installation.

**Status.** *Configured*: the rule's values come from configuration. *Fixed*: the method is in code, and its
inputs are configured. *DVR*: LEGAL / DOMAIN VERIFICATION REQUIRED, meaning PRIME applies the stated default until
the Provincial Assessor confirms or changes it.

## 1. Market value

| # | Rule | Basis | Effective date, jurisdiction | System representation | Status |
|---|---|---|---|---|---|
| R1 | **Land:** each strip's market value = area × the SMV unit value for its classification, sub-class, zone and use; plus the value adjustment = base × the sum of the adjustment percents; × the legacy location factor where the land still has one; then the schedule's minimum and maximum | LGC §199, §201; LAM Bk III Ch. II (land appraisal); MRPAAO Att. 1 | The SMV in force on the valuation date covering the municipality | `ValuationCalculator.CalculateLandStrip`; `SmvSchedule`; `Land.LocationFactor` | Fixed. DVR: whether factors add (as built) or compound |
| R2 | **Adjustment factors** by rule kind: flat, corner lot, by road type, by distance to the poblacion or an all-weather road (bands read "over the lower, not over the upper"), by depth band (never for subdivision lots). A factor that cannot apply stops the valuation with the reason | LAM Bk III pp.76–78 | Approved factor of the SMV pricing the strip, in force on the date | `AdjustmentFactor`, its rows; `AdjustmentRules.Evaluate` | Configured |
| R3 | **Adjustments that deduct more than the whole value** (sum below −100 %) are refused (`ADJUSTMENT_TOTAL_OUT_OF_RANGE`), not floored at zero. A sum of exactly −100 % gives zero | No rule found; a market value is never negative | — | `ValuationService` (land strips) | Fixed. DVR: whether a floor applies |
| R4 | **Trees, plants and other land improvements:** number × the SMV rate for their kind, within the schedule's limits | MRPAAO Att. 1; LAM Bk III Ch. II | As R1 | `ValuationCalculator.CalculateImprovement` | Fixed |
| R5 | **Building on the SMV's construction cost:** per use portion, core = floor area × the base unit construction cost for the structural type and kind; + extra items priced from the SMV; × completion percent; − depreciation = that × the table's percent for the building's age; market value = the rest | LAM Bk III p.72; `valuation-foundation.md` §4.5 | The SMV in force covering the municipality | `ValuationCalculator.CalculateBuildingByCost`; `SmvBuildingCost`, `SmvExtraItemCost`, `SmvDepreciationSchedule` | Fixed |
| R6 | **Building age** = valuation year − the year completed, else constructed, else occupied; never below zero | `valuation-foundation.md` Q9 | — | `BuildingDepreciation.Age` | Fixed. DVR |
| R7 | **Depreciation table reading:** *cumulative* (the band holding the age) or *yearly within band* (each band's rate × its years, added). An age below the first band is not depreciated. An age beyond a bounded table is refused. Depreciation never exceeds 100 − the table's minimum remaining value | `valuation-foundation.md` Q8 [C3] | Per table | `SmvDepreciationSchedule.Reading`; `BuildingDepreciation.Percent` | Configured. DVR: which reading the province's SMV uses |
| R8 | **Depreciation carried over:** a building keeps the percent of its last posted valuation unless the transaction, or a general revision, gives it a new one | `valuation-foundation.md` Q10 | Per transaction type | `TransactionType` setting; `ValuationService` | Configured |
| R9 | **Building without SMV construction costs** (legacy and DEMO SMVs): floor area × rate + additional items, × completion; **not depreciated**, since no table exists to depreciate by | CLAUDE.md §5 (no invented rate) | — | `ValuationCalculator.CalculateBuildingPortion` | Fixed. DVR before any age-based step is added |
| R10 | **Machinery, brand-new:** market value = acquisition cost, including installation, freight, duties and similar charges | LGC §224(b) | — | `ValuationCalculator.CalculateMachinery` | Fixed |
| R11 | **Machinery, entered replacement cost:** replacement cost × remaining life ÷ economic life (remaining life clamped to 0…economic life); not below the minimum remaining value percent of the replacement cost while the machine is in operation | LGC §224(a), §225 | Installation-wide setting | `Valuation:MachineryMinimumRemainingValuePercent` (+ `…LegalBasis`) | Configured |
| R12 | **Machinery, derived replacement cost:** cost, insurance and freight × (exchange rate at valuation ÷ at acquisition, imported only) × (price index of the valuation year ÷ of the acquisition year), + other expenses at cost; depreciation = the smaller of years of use ÷ economic life and the yearly maximum × years of use, at most 100 %; the R11 minimum while in operation | LAM Bk III pp.73–75; LGC §225 | Approved price indices of the series for both years | `ValuationCalculator.CalculateMachineryDerived`; `PriceIndex`; `Valuation:MachineryMaximumYearlyDepreciationPercent` | Configured. DVR [C4]: whether other expenses are trended (they are not) |
| R13 | **Rules as of the valuation date:** every rate, cost, table, factor, index and level is the one in force on the valuation date. A unit is assessed only on a valuation made as of the assessment's effective date. Posted history is never recomputed under new rules | CLAUDE.md §76, §97; `valuation-foundation.md` §4.1 | The valuation date | `ValuationService` "rules as of"; `VALUATION_DATE_MISMATCH` | Fixed |
| R14 | **Independent appraisal:** a row valued by an approved independent appraisal takes the appraised value, or its floor-area or land-area share; nothing is computed from the appraisal's inputs | `valuation-foundation.md` §4.7, Q13 | — | `IndependentAppraisal`; `ValuationCalculator.FromIndependentAppraisal`, `SpreadByArea` | Fixed |

## 2. Rounding and amounts

| # | Rule | Basis | Effective date, jurisdiction | System representation | Status |
|---|---|---|---|---|---|
| R15 | **Rounding step:** a configured step (e.g. the nearest ten) applied to each row's market value, half away from zero, recording the value before rounding | Only the superseded MRPAAO rounds land to the nearest ten (baseline Q8) | Installation-wide setting, off by default | `Valuation:MarketValueRoundingStep` (+ `…LegalBasis`, required when set) | Configured. DVR [C5] |
| R16 | **Centavo rounding:** every row's market value is rounded to the centavo, half away from zero, after any step and before it is stored or assessed. The valuation's total is the sum of the rounded rows. Intermediate breakdown values keep full precision; when rounding changed a value, the breakdown keeps the value before it | No rule in RA 12001, its IRR, the LGC or the PVS (baseline Q8); PRIME stores pesos as `numeric(18,2)` (CLAUDE.md §65) | All | `Money.ToCentavo`; `ValuationCalculator.ToCentavo` | Fixed. DVR: half away from zero vs. another rule |
| R17 | **Assessed value** of each assessment line = market value × level ÷ 100, rounded to the centavo half away from zero. The unit's assessed value is the sum of its lines | LGC §218 (levels); rounding: as R16 | — | `AssessmentService.AssessLinesAsync` | Fixed. DVR as R16 |
| R18 | **Amount limit:** an amount larger than `numeric(18,2)` holds (9,999,999,999,999,999.99), or than the computation can hold, is refused with 400 `VALUE_OUT_OF_RANGE` and nothing is saved | CLAUDE.md §65, §106 | All | `Money.Checked`; `ValueOutOfRangeException`; `ExceptionHandlingMiddleware` | Fixed |

## 3. Assessment

| # | Rule | Basis | Effective date, jurisdiction | System representation | Status |
|---|---|---|---|---|---|
| R19 | **Assessment lines:** valuation rows are grouped by classification and actual use. Each group is one line at its own level. A row without its own classification or use takes the Tax Declaration's | `mrpaao-forms-model.md` §8.2 | — | `AssessmentService.AssessLinesAsync` | Fixed |
| R20 | **Level brackets** read "over the lower value, not over the upper"; a lower value of 0 includes 0; the upper value may be open. The bracket value is the line's market value, or the unit's total | LGC §218 table form; `value-and-assess.md` §2.6 | The approved level in force on the effective date | `AssessmentLevel`; `Assessment:LevelBracketBasis` (`Line` default, `Unit`) | Configured. DVR [C6]: the ordinance's bracket wording and basis |
| R21 | **Taxable or exempt:** a line is exempt when an approved exemption covering it is in force on the effective date; exempt lines are still appraised, assessed and listed | LGC §234; `assessment-listing-exemptions.md` §4.1 | The exemption's dates | `PropertyExemption`; `ExemptionTaxability.MarkAsync` | Configured |
| R22 | **Effectivity** derived by the transaction type's rule (next January, next quarter, fixed or by periods), with its legal basis on the type; an override needs a reason. An approval that would change the derived date is refused | `valuation-foundation.md` §4.1; transaction type's configured legal basis | The transaction type in force | `TransactionType.EffectivityRule`; `EffectivityRules` | Configured |
| R23 | **Back taxes:** each period since the start is valued under the SMV of that period and assessed at its level; the start is limited to the configured number of years | `valuation-foundation.md` (back taxes); configured legal basis | Per period | `Valuation:BackTaxYearsLimit` (+ `…LegalBasis`), `BackTaxBuildingRules`, `BackTaxMachineryRules`; `BackTaxPeriods` | Configured |

## 4. Changes

| Date | Change |
|---|---|
| 2026-10-09 | Created (H3). R16 and R18 are new: rows were rounded only by the database on save, so a general revision (which values and assesses in one request) could assess an unrounded value while a manual assessment of the same valuation used the stored one, and stored rows did not always add up to the stored total. R3 is new: such adjustments used to fail with a server error at the database's check |
