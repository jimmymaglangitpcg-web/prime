# L7 — Assessment appeals (design)

| | |
|---|---|
| Step | Phase 10, step L7 (CLAUDE.md §97, §113); the gap analysis's step 10f |
| Status | **Deferred** (user decision, 2026-10-06). Design kept; Q1–Q17 to be decided when the step is resumed, no code before (CLAUDE.md §108) |
| Sources | LAM 2025 Book III Ch. VI (pp.101–102); Book I (the assessor attends LBAA sessions); RA 7160 §§226–231; MRPAAO Ch. VII (rules of procedure before the LBAA and CBAA, used where the LAM is silent); the regulatory baseline (`current-real-property-regulatory-baseline.md`, appeals row) |
| Depends on | L3 (transactions, annotations, taxability), L5 (NOA service and appeal deadline, annotation carry-over), L6-6 (general revision completion report), LP (offices and jurisdiction) |
| Feeds | L8 (treasury interface: payment-under-protest reference, revised-assessment events); Phase 11 (appeal statistics in BLGF reports) |

Committed text cites the LAM and the MRPAAO by chapter and page and paraphrases them (CLAUDE.md §118). Periods are
settings with their legal basis, never constants in code.

## 1. Scope

Finding K2 of the gap analysis (untracked, `docs/lam/`): PRIME records the Notice of Assessment's appeal deadline but
keeps no record of an appeal. CLAUDE.md §113 asks for the appellant, the grounds, the dates filed and decided, the
board's decision and any further appeal; and, when a decision requires it, a revised assessment created through the
normal workflow without overwriting the original. §50 lists Appeals on the Property Profile; §55 lists pending appeals
on the dashboard; §111 asks whether an assessment was appealed, and with what result.

**In scope:** appeals against the **assessor's action in an assessment** (the assessor is the respondent), their
hearings, decisions at each level, the assessor's own further appeal, and carrying out a decision.

**Out of scope:** appeals against the **treasurer's** action (refunds, tax credits, collection, special levies). The LBAA
hears them too (LAM p.102 §3–§4), but they are treasury matters (CLAUDE.md §0). PRIME does not record them.

## 2. What the sources require (paraphrased)

**Who and when (LAM p.101; LGC §226).** An owner, or anyone with a legal interest in the property, who is not satisfied
with the assessor's action may appeal to the LBAA of the province or city within 60 days from receiving the written
notice of assessment. The petition is under oath, in the prescribed form, with copies of the tax declarations and the
supporting affidavits or documents.

**What the petition states (MRPAAO Ch. VII Rule 2 §7).** The action appealed from, the grounds, the arguments, and the
date the petitioner received the notice of assessment. The petitioner is the owner; the respondent is the assessor (§5).

**Payment under protest (LAM p.101 §2; MRPAAO Ch. VII Rule 2 §3).** No appeal is entertained unless the tax is first paid
and the receipt is annotated "paid under protest". Where the delinquency equals the tax paid in previous years, a surety
may be posted in lieu of cash, under conditions. The MRPAAO (Rule 2 §9) lets the board entertain the appeal but defer
the hearing until the tax is paid or the bond posted. The case law the LAM cites treats payment as a condition of
entertaining the appeal.

**No suspension (LAM p.101; LGC §231).** An appeal does not suspend collection of the tax as assessed; the tax is
adjusted later according to the final outcome.

**The board (LAM p.101 §1; LGC §227).** The Registrar of Deeds (chair), the provincial or city prosecutor and the
provincial or city engineer, ex officio; substitutes when absent.

**The assessor attends (LAM p.102 §5; Book I).** The assessor attends, personally or through a representative, every
session where their assessment is the subject, and gives the board the records it needs.

**Decision (LGC §229; MRPAAO Rule 2 §10–§12).** The LBAA decides within 120 days from receipt of the appeal, on
substantial evidence. The board's secretary serves the decision on the parties. If the assessor concurs in a revision of
the assessment, the assessor notifies the petitioner in the prescribed form.

**Further appeal (LAM p.102 §6–§7; MRPAAO Rule 3).** Any party — owner, assessor or treasurer — dissatisfied with the LBAA
decision may appeal to the CBAA within 30 days of receiving it. The CBAA has exclusive appellate jurisdiction, decides
within 12 months, and its decision becomes final 15 days after receipt unless reconsideration is sought (one petition for
reconsideration, within 15 days). The appellant may withdraw at any time before resolution. Review beyond the CBAA (Court
of Tax Appeals, Supreme Court) is outside the manuals.

## 3. What PRIME has

| Area | PRIME today | Gap |
|---|---|---|
| Appeal period | `NoticeOfAssessment.AppealPeriodDays` (setting `Notices:AppealPeriodDays` with its legal basis), `ReceivedDate`, `AppealDeadline` frozen when service is recorded; shown on the RPU's assessments and in the general revision records | No appeal record at all |
| Revised assessment | Transactions of configurable types (reassessment, correction, court order) produce a new assessment and TD through the normal maker-checker workflow; effectivity by the type's rule (`NextJanuary`, `NextQuarter`, `Periods`, `Fixed`); posted history is never changed | No link from a transaction to an appeal decision |
| Values outside the SMV | `IndependentAppraisal` (L1-7) records a value the appraiser fixed outside the SMV | Not tied to a board's decision |
| Annotations | Configurable annotation types on TDs, carried over (L5-4), lifted with a reason; `BlocksCancellation` (L3-4) | No "under appeal" annotation |
| Documents | `Document` metadata (any related entity), private storage | — |
| Reports | General revision completion report says appeals are "recorded from L7" (placeholder); the dashboard has no appeal count | Counts missing |
| Offices | Records scoped by the property's municipality; provincial office sees all (LP) | — |

## 4. Proposal

### 4.1 L7-1 — appeal records

An **assessment appeal** (`AssessmentAppeal`) is the owner's petition against one or more posted assessments of one
property, followed through every level it reaches.

- **Petition:** property; appellant (a party of the property, or a name with a capacity: owner, administrator,
  person with legal interest; Q2); counsel (optional); date filed and the board's docket number; the date the appellant
  received the NOA; grounds and the relief sought (text); the appellant's claimed values per item (optional).
- **Items** (`AssessmentAppealItem`): the TDs and posted assessments appealed, and the NOA served for each (Q3).
- **Filing checks, as warnings recorded on the appeal, never refusals** (Q4): filed after the NOA's appeal deadline;
  no payment-under-protest or surety reference; an item whose NOA was not served. The board decides admissibility;
  PRIME records what it was told.
- **Payment under protest** (Q5): receipt number, date and amount, or the surety bond reference, entered from the copy
  the appellant or the treasurer gives (L0-4 E2). PRIME does not read treasury records (CLAUDE.md §0).
- **Levels** (`AssessmentAppealLevel`): LBAA first, then CBAA if a party appeals, then "court" (Court of Tax Appeals or
  higher) recorded in the same shape (Q6). Each level records who appealed (owner, assessor, treasurer), the date
  filed and docket number, the deadline to decide (from settings), the decision: date, date the assessor received it,
  outcome, summary, and the values the board fixed. It also records whether reconsideration was sought, and the date
  the decision became final.
- **Outcomes** (Q7): affirmed (appeal denied), revised (granted in whole or part), dismissed (e.g. filed late or
  unpaid), withdrawn, remanded to the assessor.
- **Proceedings** (`AssessmentAppealEvent`): hearings, ocular inspections, records submitted, the assessor's comment
  or answer, attendance (the assessor or the named representative), with dates and notes (LAM p.102 §5).
- **Documents:** petition, receipt or bond, decision, notices — `Document` rows related to the appeal.
- **Status** follows from the levels: filed → pending at LBAA → decided → (pending at CBAA → decided …) → final; or
  withdrawn. Final means no further appeal is pending and the last decision's finality date has passed (or was entered).
- **Deadlines from settings** (`Appeals:*`, each with its legal basis; Q8): LBAA decision 120 days from receipt; further
  appeal 30 days from receipt of the decision; CBAA decision 12 months; finality 15 days; reconsideration 15 days.
  PRIME shows them and lists what is overdue or about to lapse. It never closes a case by itself.
- **The assessor's own further appeal** (Q9): when the LBAA revises against the assessor, the worklist shows the
  30-day window for an appeal to the CBAA by the assessor.
- **"Under appeal" annotation** (Q10): a configured annotation type, added to each appealed TD when the appeal is
  recorded and lifted (with the outcome as reason) when it becomes final. It informs; it does not block (an appeal does
  not suspend the assessment; LGC §231).
- **Offices** (Q11): an appeal belongs to the property's municipality. The provincial office records and edits
  (the provincial assessor is the respondent before the provincial LBAA); municipal offices view the appeals of their
  jurisdiction and may record proceedings they attended.

### 4.2 L7-2 — carrying out a decision

- A final decision that revises the assessment is carried out by a **transaction** of a configured type (a
  reassessment- or correction-kind type, e.g. "Appeal decision"), filed on the property and linked to the appeal level
  whose decision it implements (`PropertyTransaction.AssessmentAppealLevelId`; Q12). The decision is a mandatory
  requirement of the type, like a court order (L3-3). The transaction goes through the normal valuation, assessment,
  maker-checker and TD workflow; nothing posted is changed.
- **Values the board fixed** (Q13): where the decision fixes a market value instead of correcting an input (class,
  area, use, factor), the appraiser records it as an independent appraisal citing the decision (L1-7), so the
  calculation still shows where the value came from.
- **Effectivity** (Q14): by the type's rule. PRIME's recommendation is `Fixed`, at the appealed assessment's
  effectivity, so the revised assessment replaces the appealed one from the same date. The treasurer then adjusts the
  tax (LGC §231). DOMAIN VERIFICATION REQUIRED.
- **Notice** (Q15): the revised assessment gets its own NOA through the existing notice process. It serves as the
  notice to the petitioner of the revised assessment (MRPAAO Rule 2 §12) until the province supplies another form.
- When the implementing transaction's assessment is posted, the appeal shows **carried out** with the new assessment.
  Affirmed and dismissed appeals need no transaction.

### 4.3 L7-3 — views and counts

- **Property Profile:** an Appeals section (CLAUDE.md §50) — each appeal, its items, levels, decisions, and the
  implementing transaction.
- **Appeals page:** list by status, level, municipality and due date, with the worklists of §4.1. A provisional
  **register of assessment appeals** printable through the forms foundation (Q16), watermarked until the province
  supplies a layout.
- **Dashboard:** pending appeals (CLAUDE.md §55).
- **General revision completion report (L6-6):** appeals filed against the revision's assessments, by outcome, in
  place of the placeholder.
- **Assessment history:** each assessment shows if it was appealed and the outcome (§111).

## 5. Data and migrations

Additive only; nothing existing is dropped or rewritten.

- `AssessmentAppeals`, `AssessmentAppealItems`, `AssessmentAppealLevels`, `AssessmentAppealEvents`.
- `PropertyTransactions.AssessmentAppealLevelId` (nullable).
- Settings `Appeals:*` (periods with legal bases, the annotation type code). No LGU data in the repository; DEMO
  appeals only.

## 6. Delivery steps

| Step | Content | Size |
|---|---|---|
| L7-1 | Appeal records: petition, items, payment reference, levels, decisions, proceedings, documents, warnings, deadlines, annotation; API; Appeals page and Property Profile section | M |
| L7-2 | Carrying out a decision: linked transaction type, independent appraisal from a decision, effectivity, NOA, "carried out" status | S |
| L7-3 | Register (provisional form), dashboard count, GR completion report, assessment history | S |

## 7. Exit criteria

1. A DEMO appeal against two TDs of one property is recorded with the payment-under-protest reference; a second,
   filed after the NOA deadline and without a receipt, is recorded with both warnings.
2. Hearings with the assessor's attendance and an ocular inspection are recorded; the LBAA decision date and its
   120-day deadline are shown; an overdue decision is listed.
3. An LBAA decision revising one TD's market value: the assessor sees the 30-day window to appeal to the CBAA; after it
   passes the decision is final; a transaction linked to the decision produces a revised assessment and TD through the
   normal approvals, with the original assessment untouched; the NOA of the revision is issued; the appeal shows
   carried out and the annotation is lifted.
4. A second DEMO appeal goes to the CBAA by the owner and is withdrawn there.
5. The Property Profile, the appeals register, the dashboard and the GR completion report show the appeals.

## 8. Review questions

| # | Question | Recommendation |
|---|---|---|
| Q1 | Appeals against the treasurer (refunds, credits, collection, levies) | Out of scope (treasury, CLAUDE.md §0); PRIME records only appeals against the assessor's action |
| Q2 | Who the appellant is | A party of the property where there is one; otherwise a name with a capacity (owner, administrator, legal interest), since the law allows anyone with a legal interest |
| Q3 | What one appeal covers | One property, one or more of its posted assessments/TDs, each with its NOA |
| Q4 | Late filing, missing payment under protest, unserved NOA | Recorded as warnings on the appeal, never refusals: the board decides admissibility |
| Q5 | Payment under protest [L0-4 E2] | The receipt (number, date, amount) or surety bond reference entered with the appeal; no treasury link until L8 |
| Q6 | Levels of appeal | LBAA, CBAA, then "court" (CTA or higher) in the same record shape; each level has its own appellant, dates, decision and finality |
| Q7 | Outcomes | Affirmed, revised, dismissed, withdrawn, remanded |
| Q8 | Periods | Settings with legal bases: LBAA decision 120 days, further appeal 30 days, CBAA decision 12 months, finality and reconsideration 15 days; calendar days (DOMAIN VERIFICATION: calendar or working days, and whether RA 12001's IRR changes any) |
| Q9 | The assessor's further appeal | Shown as a worklist entry with the 30-day window when the LBAA revises; recorded as a CBAA level with the assessor as appellant |
| Q10 | "Under appeal" annotation | Yes: a configured annotation type, added on recording, lifted when final; informative, not blocking |
| Q11 | Which office records appeals | The provincial office records and edits; municipal offices view their jurisdiction's appeals and record proceedings they attended |
| Q12 | How a decision is carried out | A transaction of a configured type linked to the deciding level, the decision a mandatory requirement; normal workflow; nothing posted changed |
| Q13 | A market value fixed by the board | Recorded as an independent appraisal citing the decision (L1-7) |
| Q14 | Effectivity of the revised assessment | Type rule `Fixed`, at the appealed assessment's effectivity (DOMAIN VERIFICATION REQUIRED) |
| Q15 | Notice to the petitioner of the revision | The revised assessment's own NOA, until the province supplies another form |
| Q16 | Appeals register | A provisional, watermarked register through the forms foundation; the province's layout as content when supplied |
| Q17 | Board members | Not recorded as configuration; a decision records the board's name and docket only (the composition is fixed by law and changes with office holders) |

## 9. Implementation log

### Deferred (2026-10-06)

The user deferred L7. Nothing else depends on it: the rest of PRIME runs without appeal records, and the step is
additive, so it can be resumed later without rework. Until then:
- A decision that revises an assessment is carried out with a reassessment or correction transaction whose type requires
  the decision as a supporting document; the original assessment is untouched.
- A TD may be marked with a configured annotation type ("under appeal") by hand.
- The NOA's appeal deadline is shown as before.
- The general revision completion report keeps its placeholder; the dashboard has no appeal count.

Consequences: CLAUDE.md §110 objective 20 and the §111 question on appeals stay open; appeal history is kept outside
PRIME and would need importing when L7 is built. Resume before go-live if the province wants appeal history in PRIME.
