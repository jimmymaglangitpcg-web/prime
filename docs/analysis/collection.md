# PRIME — Phase 9 Collection (design for review, 2026-09-26)

The goal (docs/DEVELOPMENT-ROADMAP.md Phase 9; CLAUDE.md §40, §41, §53,
§66, §75): a cashier can take payment against posted bills, the payment is
allocated line by line and receipted, a payment can be voided, reversed or
corrected under maker-checker, and collections can be reported and
reconciled. Exit criteria: the CLAUDE.md §74 end-to-end flow passes through
PAY → VERIFY BALANCE; overpayment, partial payment and reversal (§75) are
tested; posting is shown to be atomic under concurrent submission.

Every rate, account code, receipt format and cut-off in this design is the
LGU's or BLGF/COA's to supply. PRIME invents none: the development database
gets DEMO values only, and each engine choice made without a legal source is
marked **DOMAIN VERIFICATION REQUIRED**.

## 1. What exists, and the gaps

| Area | Exists | Gap for collection |
|---|---|---|
| Bills | `TaxBill` per RPU and tax year: Draft → Posted → Cancelled; a new posted bill supersedes the old one. Lines per installment × tax type × component (tax, discount, penalty, interest), each naming its rule and frozen rate | A bill is priced **as if paid in full on its as-of date**. A payment on any other date needs its discount, penalty and interest recomputed for that date, and for the unpaid part only |
| Engine | `BillingCalculator` (pure): discounts, penalty and interest per installment on the installment's **whole** tax | Nothing works on an outstanding amount; nothing knows what is already paid |
| Numbering | `NumberedDocumentKind.OfficialReceipt` exists; `NumberingScheme` + atomic `NumberSequence`; manual entry optional per scheme | No receipt, and no transaction number (eOR needs one separate from the OR number) |
| Forms | `FormDefinition`/`IssuedForm`: frozen HTML and JSON snapshot, issued once per subject | No receipt form |
| Tax types | `TaxType` lookup (basic RPT, levies) | No revenue account or fund coding. The eOR must show amounts "coded to subsidiary-ledger revenue classification" (DOF DO 054-2024 §7.1) |
| Statement of Account | Lists posted bills and their totals | No paid or balance column |
| Maker-checker | Real for assessments and rules; dev-only `X-Prime-Dev-Act-As: checker` header | Nothing for payment reversal (CLAUDE.md §46 names it) |

## 2. What is owed: principal comes from the bill, charges come from the payment date

The unit that is owed is one **installment of one tax type** of one RPU and
tax year:

```
key = (RpuId, TaxYear, InstallmentSequence, TaxTypeId)
principal owed   = the Tax line for that key on the POSTED bill
principal paid   = Σ principal allocations to that key from payments that still count
outstanding      = principal owed − principal paid
```

Keying on the installment, not on a bill line, means a recomputed bill that
supersedes the old one keeps its payments without migrating anything. If a
new bill drops the principal **below** what was already paid, the key shows
as overpaid. It is listed on the Statement of Account for the Treasurer, and
refunds and credits stay out of scope (§8).

**Charges follow the principal being paid.** When principal P of a key is
paid on date d:

- **On or before the due date:** discounts, only if P settles the key in full
  (a discount on part of an installment is not assumed).
- **After the due date:** penalty and interest are computed on P only, with
  interest months counted from the due date to d.

The unpaid remainder keeps accruing and is charged when it is paid. Nothing
is charged twice, and each payment reproduces from its own lines. The rules
used are the ones **frozen on the posted bill** (its rule ids, resolved as of
the bill's `RulesAsOfDate`), not whatever is in force on the payment date.

**Single source of the arithmetic (CLAUDE.md Rule 9):** `BillingCalculator`'s
discount, penalty and interest steps are extracted into shared methods that
take a base amount. The bill uses them with the installment's tax, and a new
pure `CollectionCalculator` uses them with P. The 46 existing billing tests
must still pass unchanged.

## 3. Proposed model

```
PaymentMode (lookup)   Code, Name, RequiresReference, RequiresBank
                       DEMO: CASH, CHECK, ONLINE — the LGU's list is configurable

RevenueAccountMapping  (effective-dated configuration, approved like billing rules)
                       TaxTypeId, Component (Tax|Penalty|Interest|Discount),
                       YearCategory (Current|Prior|Advance),
                       AccountCode, AccountName, Fund
                       — the LGU chart of accounts: DOMAIN VERIFICATION REQUIRED

Payment                TransactionNumber   unique, system-generated (eOR §7.1)
                       OfficialReceiptNumber  unique; OfficialReceipt numbering scheme,
                                              or typed from a pre-printed accountable
                                              form when the scheme allows manual entry
                       IdempotencyKey      unique, sent by the client (§66)
                       PayorTaxpayerId?, PayorName (frozen), PayorAddress (frozen)
                       PaymentDate (LGU local date), ReceivedAt (date AND time — §7.1)
                       OfficeCode, LocationCode (frozen from Lgu:* configuration)
                       CashierUserId, Total (= Σ allocations, checked),
                       AmountTendered, Change
                       Status: Posted → Voided | Reversed
                       VoidOrReversal: Kind, Reason, RequestedBy/At, ApprovedBy/At,
                                       TransactionNumber (its own — §7.1)
                       ReplacesPaymentId?  (a correction = void + reissue)
                       RemittanceId?

PaymentTender          PaymentId, PaymentModeId, Amount, Reference, Bank, CheckDate

PaymentAllocation      PaymentId, TaxBillId (the posted bill at the time),
                       RpuId, TaxYear, InstallmentSequence, TaxTypeId, DueDate,
                       Component, RuleId, RatePercent, BaseAmount, Months, Amount,
                       Explanation, YearCategory, AccountCode, Fund   (all frozen)

Remittance             CashierUserId, CollectionDate, number, totals by mode and fund,
                       Status: Draft → Submitted → Accepted (a different user accepts)
```

Constraints: amounts are `numeric(18,2)`; a posted payment's total equals
the sum of its allocations and of its tenders less change; only discount
allocations are negative; unique transaction number, OR number and
idempotency key; a void or reversal must be approved by someone other than
the requester.

**Concurrency (CLAUDE.md §66):** posting runs in one database transaction
that first locks the posted bill rows for every (RPU, tax year) it touches
(`SELECT … FOR UPDATE`). It then re-reads what is outstanding, checks the
client's expected total, and writes. Bill post and cancel take the same lock.
Two cashiers paying the same installment: one succeeds and the other gets
`PAYMENT_ALREADY_SETTLED` (409). The same idempotency key sent twice returns
the first payment unchanged. OR and transaction numbers are drawn inside the
same transaction, so a failed post uses up no number.

**Bills with payments:** a bill cannot be cancelled without a replacement
while payments count against it (`BILL_HAS_PAYMENTS`). It can be superseded,
since payments follow the key (§2).

## 4. Payment flow and API

1. **Outstanding:** `GET /api/properties/{id}/outstanding?asOf=` lists, per
   RPU, year and installment, the principal owed, paid and outstanding, and
   the charges if paid on `asOf`. A taxpayer variant covers all of their
   properties.
2. **Quote:** `POST /api/payments/quote {payor, paymentDate, items[]}`, where
   each item is `{rpuId, taxYear, installmentSequence}` for a whole
   installment, or adds `principalAmount` for part of one. It returns the
   allocations and total and saves nothing.
3. **Post:** `POST /api/payments {idempotencyKey, …quote inputs, tenders[],
   expectedTotal}`. The server recomputes the quote under the lock and
   refuses with `PAYMENT_QUOTE_CHANGED` if the total differs. It assigns the
   transaction and OR numbers, issues the receipt form and returns it.
4. **Void or reversal:** `POST /api/payments/{id}/void-request {kind,
   reason}`, then `…/approve` or `…/reject` by another user. Void means
   before remittance; reversal means after it (for example a dishonoured
   check), and a reversal is recorded in the remittance period in which it
   happens. Either way the allocations stop counting, so the balances return.
5. **Correction:** `POST /api/payments/{id}/correct` voids the payment and
   posts its replacement in one approved step (`ReplacesPaymentId`).
6. **Reads:** `GET /api/payments/{id}`, `GET /api/properties/{id}/payments`,
   and `GET /api/payments?date=&cashierId=&status=`.
7. **Collection:** `GET /api/collections/summary?from=&to=&groupBy=cashier|taxType|taxYear|fund|mode|barangay`,
   and remittances: create, submit, accept. **Reconciliation** checks that
   allocations = tenders − change = the remittance total, for each cashier
   and day. Any mismatch is listed, never hidden.

Payment date: the LGU's local date (`IClock.LocalDate`). **No back-dating**
unless a later phase adds a permission for it.

Advance payment: a year not yet due can be paid **only against a posted bill
for that year** (bills can already be generated ahead). Any advance discount
comes from the rules frozen on that bill.

Overpayment: the amount tendered may exceed the total, and the difference is
recorded as change. Money is never applied beyond what is outstanding, and
unapplied credit is not held (§8).

## 5. The receipt

A provisional `OFFICIAL_RECEIPT` form (PRIME watermark; the official layout
is COA/BLGF's and is **DOMAIN VERIFICATION REQUIRED**), issued through the
Forms Foundation so the printed receipt is frozen. It carries the eOR minimum
content of DOF DO 054-2024 §7.1:

- issuing office and location code
- payor
- date and time
- nature of collection, with each line's amount and account code
- OR number
- transaction number
- mode of payment
- the bill number or TD number, as the "Order of Payment / Assessment Number"

The BLGF eOR numbering format (§7.2) is not yet issued. Until it is, the OR
number comes from a configurable numbering scheme (DEMO pattern in dev).

## 6. Screens

- **Payment workspace** (`/collection/pay`, CLAUDE.md §53): find a property
  or taxpayer, tick installments in the outstanding table (oldest first by
  default), quote, add tenders (amount, mode, reference), confirm in a
  dialog, then post and print the receipt.
- **Payments tab** on the Property Profile: the property's receipts, with
  status and void/reversal history.
- **Statement of Account:** adds paid, balance, and charges as of today.
- **Collections page** (`/collection`): the day's receipts per cashier, void
  and reversal requests waiting for approval, remittances, the summary
  report and reconciliation, all printable and exportable as CSV (PDF/Excel
  in Phase 11).

## 7. Delivery (checkpointed like earlier phases)

| Step | Content | Verified by |
|---|---|---|
| 9a | Extract the charge steps; `CollectionCalculator` (outstanding, charges on P, allocation order, rounding, last-piece remainder) | Existing 46 billing tests unchanged, plus new §75 unit tests: zero, large amounts, decimals, rounding, partial, full, overpayment (as change), multi-year, advance, penalty, interest, discount |
| 9b | Entities, migration (additive), `PaymentService`, quote/post/read API, DEMO modes and account mappings | Integration tests: pay → balance, idempotent repeat, **parallel posting of the same installment** (exactly one wins), stale quote refused, bill cancel refused with payments, supersession keeps payments |
| 9c | Void, reversal and correction with maker-checker | Integration tests: self-approval refused; balances restored; reversal after remittance |
| 9d | Receipt form, payment workspace, Payments tab, Statement of Account balance | Production `tsc -b` build, plus a live browser run |
| 9e | Remittance, collection summary, reconciliation, Collections page | Integration tests and a browser run |
| 9f | The §74 end-to-end flow CREATE PROPERTY → … → PAY → VERIFY BALANCE | Automated integration test, plus one Playwright run through the UI |

Migrations go to the local dev database; Supabase follows when you ask for
a push, as before.

## 8. Out of scope for Phase 9 (flagged, not dropped)

- Payment under protest (LGC §252)
- Refunds and tax credits (§253), including holding overpaid keys as credit
- Compromise, levy, auction, forfeiture and redemption
- Delinquency aging (Phase 10)
- EPCS/online payment-channel integration with a BSP-registered operator (S7 §6)
- Real role gating (Phase 12; today only maker-checker is enforced)
- PDF and Excel exports (Phase 11)

## 9. Open for review

1. **Charges follow principal (§2):** penalty and interest are charged only
   on the principal being paid, from its due date to the payment date.
   Recommended. The alternative ("settle all accrued charges first, then
   principal") needs a "charges paid through" date per key and is harder to
   audit.
2. **Partial payments:** allow a part of one installment (`principalAmount`),
   or only whole installments? Recommended: both, with whole installments as
   the default in the UI. For a part, the cashier enters the principal and
   the system adds its charges; the principal is split across tax types in
   proportion, and the last tax type takes the remainder.
3. **Discount on part of an installment:** none. Only a payment that settles
   the key on time gets its discount. Recommended.
4. **Default allocation order** when the cashier does not pick: oldest tax
   year, then installment, then tax type in bill order. Recommended.
   (DOMAIN VERIFICATION REQUIRED: no source for an order was retrieved.)
5. **Missing revenue account mapping:** refuse to post
   (`PAYMENT_ACCOUNT_NOT_MAPPED`) so every receipt meets eOR §7.1, or post
   with the code blank and flag it? Recommended: refuse.
6. **Pre-printed receipts:** allow a typed OR number when the numbering
   scheme permits manual entry (existing `AllowManualEntry`)? Recommended:
   yes. Many LGUs still issue serially numbered paper accountable forms.
7. **No back-dating** of the payment date in Phase 9. Recommended.
8. **Remittance in Phase 9 (step 9e):** a lightweight cashier remittance,
   submitted and then accepted by another user, which also separates a void
   (before remittance) from a reversal (after)? Recommended: yes. The
   alternative is to leave remittance to Phase 11 and treat every
   cancellation as a void.

### 9.1 Decisions (user, 2026-09-26: "yes")

All eight recommendations are accepted as written above. Each remains
DOMAIN VERIFICATION REQUIRED where §9 says so, and can be changed in one
place (`CollectionCalculator` for 1–4, `PaymentService` for 5–8).

## 10. Implementation status

**9a done (2026-09-26, uncommitted):**
- `BillingCharges` (Domain, internal) holds the discount, penalty and interest
  steps, which take a base amount. `BillingCalculator` now calls it, and its
  lines and wording are unchanged: the 46 billing tests pass as before.
- `CollectionCalculator` (pure) with `CollectionInput`/`CollectionBill`/
  `CollectionKey`/`CollectionItem`/`CollectionAllocation`, plus the
  `CollectionYearCategory` enum. Details beyond §2 that 9a settled:
  - A whole-year advance discount needs the whole year of that tax type
    settled by **this one payment**; a key partly paid before gets no discount.
  - A fixed-amount penalty is charged once per key (`FixedPenaltyCharged`);
    a percentage penalty applies to each principal paid late.
  - A partial amount equal to everything still owed on the installment counts
    as a whole payment, and its discount is given.
  - A key paid beyond a lowered bill has `Outstanding` 0, so selecting it is
    refused as already settled.
- Tests: `tests/Prime.Domain.Tests/DomainServices/CollectionCalculatorTests.cs`,
  31 cases, all DEMO values: full, partial, rounding remainder, centavos,
  large amounts, each discount, penalty, interest and its cap, charges on the
  part paid only, fixed penalty once, multi-year order and categories, advance
  payment, and every refusal. Over-tendering is handled as change by the
  service (9b). Domain tests: 132 passing; the solution builds.

Next: 9b (entities, migration, `PaymentService`, quote/post/read API,
integration tests including concurrent posting).

**9b done (2026-09-26, uncommitted):**
- Entities in `Prime.Domain/Entities/Collection/`: `Payment`, `PaymentTender`,
  `PaymentAllocation`, `PaymentMode` and `RevenueAccountMapping`, plus the
  `PaymentStatus` enum. Migration `Payments` is additive (5 new tables) and
  is applied to the local dev database only.
- Database constraints:
  - unique transaction number, OR number (every status) and idempotency key;
  - tendered = due + change, and the amount due is positive;
  - discounts are the only negative allocation lines;
  - one open approved account mapping per (tax type, component, year category).
- `NumberedDocumentKind.PaymentTransaction` (8) for the transaction number.
  Posting is refused without an approved scheme for it. The OR number comes
  from the `OfficialReceipt` scheme, or is typed when the scheme allows
  manual entry (or when no scheme exists).
- `Lgu:LocationCode` is frozen on each payment, with `Lgu:Office`.
- `ICollectionLock` is implemented as `CollectionLock`: PostgreSQL
  transaction-level advisory locks per (unit, tax year), taken in a fixed
  order. Payment posting takes it. Bill posting and cancelling take it too:
  a posted bill with standing payments can't be cancelled
  (`BILL_HAS_PAYMENTS`), but a recomputed bill can supersede it and the
  payments carry over.
- `PaymentService`:
  - It loads each posted bill, what standing payments settled, and the
    discount, penalty and interest rules in force on the bill's
    `RulesAsOfDate`.
  - `CollectionCalculator` allocates, and each line is coded to its revenue
    account (refused if unmapped).
  - On posting, it re-quotes under the lock, checks the expected total and
    the tenders, and draws the numbers in the same transaction.
  - A repeated idempotency key returns the first payment; the same key with
    a different amount is refused.
- `CollectionSetupService`: payment modes (create, list) and revenue account
  mappings (create, list, approve through `ConfigurationApproval`).
- API:
  - `GET /api/properties/{id}/outstanding?asOf=`
  - `POST /api/payments/quote`
  - `POST /api/payments`
  - `GET /api/payments/{id}`
  - `GET /api/payments?date=&cashierUserId=&status=`
  - `GET /api/properties/{id}/payments`
  - `GET|POST /api/collection/payment-modes`
  - `GET|POST /api/collection/account-mappings`
  - `POST /api/collection/account-mappings/{id}/approve`
- Error codes: `PAYMENT_ALREADY_SETTLED` (409), `PAYMENT_QUOTE_CHANGED` (409),
  `PAYMENT_IDEMPOTENCY_CONFLICT` (409), `PAYMENT_OR_NUMBER_DUPLICATE` (409),
  `PAYMENT_POST_CONFLICT` (409), `PAYMENT_BILL_NOT_FOUND` (404),
  `PAYMENT_INVALID_SELECTION`, `PAYMENT_ACCOUNT_NOT_MAPPED`,
  `PAYMENT_TENDER_INVALID`, `PAYMENT_MODE_NOT_FOUND`,
  `PAYMENT_TRANSACTION_NUMBERING_NOT_CONFIGURED`, `BILL_HAS_PAYMENTS`.
- The frontend's `NumberedDocumentKind` type and the Forms admin list gain
  `SwornStatement` (previously missing) and `PaymentTransaction`.
- Tests: `tests/Prime.IntegrationTests/CollectionFlowTests.cs`, 12 cases,
  with the LGU clock pinned through `TimeProvider`:
  - on-time payment with discount, change, receipt and transaction numbers,
    account codes, and a zero balance afterwards;
  - late payment with interest to the payment date;
  - a partial payment followed by the rest;
  - idempotency, and a stale quote;
  - each refusal;
  - a typed receipt number and its duplicate;
  - supersession keeping payments;
  - HTTP validation;
  - **concurrent posting**: two submissions with the same key and two with
    different keys at once save exactly one payment.

  The concurrency test **commits** DEMO rows to the dev database (a fresh
  property, bill, tax type, mode and mappings, reusing any receipt and
  transaction numbering in force), since uncommitted rows are invisible to a
  second connection. Full suite: 132 domain, 37 application and 194
  integration tests pass; the frontend production build passes.

9b was committed and pushed as `ce6c613`.

**9c done (2026-09-26, uncommitted):**
- `PaymentCancellation` records a request with its reason and the decision on
  it; the requester is its `CreatedBy`. There is at most one pending request
  per payment, and requests are never deleted. `Payment` gains `CancelledAt`
  and `ReplacesPaymentId` (at most one replacement per payment). Enums:
  `PaymentCancellationStatus` (Pending/Approved/Rejected) and
  `PaymentCancellationKind` (Void/Reversal). Migration `PaymentCancellations`
  is additive and applied to the local dev database only.
- Flow: request a cancellation (reason) or a correction (reason plus the
  replacement payment); **another user** approves, or rejects with a reason
  (`CANNOT_APPROVE_OWN_PAYMENT_CANCELLATION`). On approval:
  - The kind is decided by timing: **Void** if approved on the payment's own
    local date, **Reversal** otherwise. Step 9e adds "and not yet remitted" to
    the void rule.
  - The cancellation gets its own `PaymentTransaction` number (eOR §7.1).
  - The payment becomes Voided or Reversed; its allocations stay on record
    but no longer count, so the balance returns.
- A **correction** is checked when requested: the replacement is quoted as if
  the original were already undone, and the expected total and tenders are
  checked. On approval it voids or reverses the original and posts the
  replacement in the same transaction, under the collection lock.
  - The replacement is **dated like the original payment**, so the charges
    are those of that date (DOMAIN VERIFICATION REQUIRED). It gets a new OR
    number.
  - If the replacement cannot be posted at approval (e.g. an account mapping
    was withdrawn), nothing changes, even inside a caller's transaction (a
    savepoint), and the request stays pending.
- API:
  - `POST /api/payments/{id}/cancellation-requests {reason}`
  - `POST /api/payments/{id}/correction-requests {reason, replacement}`
  - `GET /api/payments/cancellation-requests?status=`
  - `POST /api/payments/cancellation-requests/{id}/approve|reject {remarks}`

  `PaymentDto` gains `CancelledAt`, `ReplacesPaymentId`,
  `ReplacedByPaymentId` and `Cancellations`.
- Error codes: `PAYMENT_NOT_POSTED`, `PAYMENT_CANCELLATION_DUPLICATE` (409),
  `PAYMENT_CANCELLATION_NOT_FOUND` (404), `PAYMENT_CANCELLATION_NOT_PENDING`,
  `CANNOT_APPROVE_OWN_PAYMENT_CANCELLATION`, `PAYMENT_CANCELLATION_CONFLICT`
  (409).
- Tests: 5 more in `CollectionFlowTests` (17 in all):
  - a void with maker-checker, its own transaction number and the balance
    restored;
  - a reversal when approved on a later day;
  - a rejection, which needs a reason, followed by a new request;
  - a correction that reissues, dated like the original;
  - a correction that fails at approval and changes nothing.

  Full suite: 132 domain, 37 application and 199 integration tests pass.

9c was committed and pushed as `e6eab47`.

**9d done (2026-09-26, uncommitted; no migration):**
- **Receipt:** `FormSubjectType.Payment` (8) and `PaymentFormDataProvider`,
  with the provisional `OFFICIAL_RECEIPT` v1 form. It shows:
  - the office, location code, OR number, transaction number, and date and
    time (new `datetime_ph` template filter);
  - the payor, and the bill or TD numbers;
  - the nature of collection by revenue account and fund;
  - the amount in words, the lines, the tenders, tendered and change;
  - a VOIDED or REVERSED banner.

  Only a posted payment can be issued. Issuing is once per payment, so a
  reprint returns the frozen receipt. The official layout (COA/BLGF eOR) is
  DOMAIN VERIFICATION REQUIRED.
- **Statement of Account:**
  - `StatementLineDto` gains `PrincipalOwed`, `PrincipalPaid`,
    `OutstandingPrincipal` and `DueAsOf`.
  - `StatementOfAccountDto` gains `AsOfDate`, `TotalPrincipalPaid`,
    `TotalOutstandingPrincipal`, `TotalDueAsOf` and `Payments` (every receipt,
    with its status).
  - `BillService` takes `IPaymentService`; there is no cycle, because payments
    do not use billing.
  - Form `STATEMENT_OF_ACCOUNT` v2 replaces the v1 layout, which assumed
    nothing was paid; the seeder installs it.
- **Frontend:**
  - `api/payments.ts`.
  - The payment workspace at `/collection/pay?propertyId=`:
    - outstanding installments to tick, with an optional partial tax amount;
    - the quote as of today, with each line's account code;
    - the payor (prefilled from the first current owner), an optional OR
      number for a pre-printed receipt, and several tenders with the change;
    - a confirmation dialog, then the result with Print receipt.
  - A **Payments** tab on the Property Profile: the receipts, a detail drawer
    (tenders, lines, cancellation history), Print receipt, and requests to
    void/reverse or to correct the payor details (same installments, amounts
    and tenders).
  - A **Collection** page (`/collection`): receipts by date, and the queue of
    pending cancellation requests with approve/reject (maker-checker; the
    dev-only act-as-checker switch works here).
  - A **Collection Setup** admin page (`/admin/collection`): revenue account
    mappings (one form can create several component × year pairs;
    maker-checker approval) and modes of payment.
  - The Statement of Account page shows paid, outstanding, due today and the
    receipts, with a Take payment button.
  - Navigation gains Collection and Collection Setup.
- **Verified in a browser** (Playwright) against the dev database, on the
  DEMO property `DEMO-BILL-AE94B8`:
  - a payment mode was created through the setup page;
  - installments 1–2 were paid late (1,090.00 = 560.00 + 530.00, matching the
    API's amounts due), with change;
  - installments 3–4 were paid on time with the discount (900.00), with
    change 100.00, and the receipt was issued with every eOR field;
  - a void was requested, refused when the requester tried to approve it,
    and approved as the dev checker; the receipt was then listed as Voided;
  - the statement showed paid 1,000.00, outstanding 1,000.00, due today
    900.00, and the voided receipt listed but not counted.

  The browser run found and fixed:
  - receipt numbers in tables were `<a>` elements without an href (not
    keyboard-reachable); they are now link buttons (CLAUDE.md §83);
  - an antd Descriptions span warning in the payment drawer;
  - a long account column that pushed the amount off-screen.
- The dev database now has DEMO account mappings for `DEMO-BASIC`/`DEMO-SEF`
  (24, approved), DEMO modes, and the payments above.
- The concurrency test now reuses one DEMO tax type, mode and mapping set
  (`DEMO-CONCURRENCY…`). Earlier runs had left six DEMO tax types, modes and
  72 mappings in the dev database; they are still there.
- Tests: 2 more in `CollectionFlowTests` (receipt issued once with the eOR
  content, and a voided payment not issuable; statement paid/outstanding/
  receipts). `BillingFlowTests` was updated for the v2 statement. Full suite:
  132 domain, 37 application and 201 integration tests pass; oxlint is clean
  and the production build passes.

9d was committed and pushed as `4261372`.

**9e done (2026-09-26, uncommitted):**
- **Remittance** (`Remittance`, `RemittanceItem`, `RemittanceModeTotal`,
  `RemittanceAccountTotal`; `RemittanceStatus` Submitted/Accepted/Returned;
  migration `Remittances`, additive, local dev database only):
  - The acting cashier remits their posted, unremitted receipts of one date in
    one step, which counts as submitted. Totals are frozen by mode of payment
    (net of change, via the pure `TenderNetting`: change comes out of the modes
    that allow it, in tender order) and by revenue account.
  - It is refused while a void or correction request on one of those receipts
    is pending (`REMITTANCE_PENDING_CANCELLATIONS`), and when there is nothing
    to remit.
  - Another user accepts it, or returns it with a reason, which frees its
    receipts; the items keep the history
    (`CANNOT_DECIDE_OWN_REMITTANCE`).
  - Optional numbering: `NumberedDocumentKind.Remittance` (9).
- **Void vs reversal** now also needs "not yet remitted": a same-day
  cancellation of a remitted receipt is a Reversal.
- **Concurrency:** `Payment.Version` is PostgreSQL `xmin` (no DDL; the
  ParcelConcurrencyToken precedent). Remitting and voiding the same receipt
  at once cannot both succeed (`REMITTANCE_CONFLICT` /
  `PAYMENT_CANCELLATION_CONFLICT`).
- **Collection summary** (`GET /api/collections/summary?from&to&groupBy=`),
  grouped by Date, Cashier, Mode, TaxType, TaxYear, YearCategory, Fund,
  Account or Barangay, over at most 366 days:
  - Collected: receipts not voided, on their payment date.
  - Reversed: negative, on the LGU date the reversal was approved.
  - Voided receipts are never counted.
- **Reconciliation** (`GET /api/collections/reconciliation?date=`), per cashier:
  receipts, amount, allocation-line total, tendered less change, remitted and
  unremitted, and voided count. It lists every disagreement:
  - allocation lines ≠ amount;
  - tendered − change ≠ amount;
  - a remittance's total ≠ its receipts, its mode totals or its account totals;
  - receipts not yet remitted.
- API: `POST|GET /api/collections/remittances`,
  `GET /api/collections/remittances/{id}`,
  `POST /api/collections/remittances/{id}/accept|return`, plus the summary and
  reconciliation above.
- UI: the Collection page gains **Remittances** (remit my receipts for a
  date; accept or return; details by mode, account and receipt), **Summary**
  (range, group-by, totals, CSV download) and **Reconciliation** (per
  cashier, Balanced / Needs attention, with the issues listed).
- Tests:
  - 4 unit tests (`TenderNettingTests`);
  - 5 integration tests: remit, accept, then only a reversal is possible;
    return frees the receipts; a pending request blocks remitting; the summary
    by tax type, mode and cashier (collected, reversed negative, voided left
    out); reconciliation before and after remitting;
  - the summary and reconciliation assertions only read each test's own tax
    types, modes and cashier, since the dev database holds committed receipts
    dated on the same pinned day.

  Full suite: 136 domain, 37 application and 206 integration tests pass;
  oxlint is clean and the production build passes.
- **Verified in a browser** against the dev database:
  - Reconciliation flagged today's unremitted receipt (OR …00007, 1,090.00).
  - Remitting worked; the cashier accepting their own remittance was refused,
    and the dev checker accepted it. The cash total was 1,090.00 net of the
    10.00 change.
  - Reconciliation then showed OK.
  - The summary showed 545.00 per tax type and 1,090.00 by mode, with the
    voided receipt left out, and the CSV downloaded.

**9f done (2026-09-26, uncommitted; no migration):**
- `tests/Prime.IntegrationTests/EndToEndFlowTests.cs` runs the CLAUDE.md §74
  flow through the application services the API calls, with their real
  workflows, in one rolled-back transaction with the clock pinned to
  15 March 2026:
  - property, then taxpayer and ownership, parcel, land RPU and land;
  - SMV, schedule and assessment level (the creator's own approval refused;
    approved by the checker);
  - valuation (MV 500,000), then assessment (AV 100,000) through submit,
    approve (the creator's own approval refused) and post;
  - the TD, submitted and approved;
  - a bill (1,800.00 with the prompt discount), posted;
  - a partial payment of 500.00, a quote of 1,500.00 for the rest (no
    discount on a part-paid installment), and payment of the rest with
    change;
  - balance 0, the statement, the receipt issued, then remit, accept and
    reconcile, with no problems.

  The dev database's approved configuration is retired inside the
  transaction, so only DEMO rules apply. It is done at service level, not
  over HTTP, because committed DEMO billing rules would supersede the dev
  database's own.
- **UI run** against the dev database: a new DEMO property
  (`DEMO-E2E-EDE9F5`) was registered, valued, assessed (MV 300,000,
  AV 60,000), posted and declared (TD approved) through the running API,
  with checker steps sent using the dev act-as-checker header. Then in the
  browser:
  - a bill was generated and posted from the Billing tab: 1,194.00, which is
    1,200.00 tax, plus 36.00 and 18.00 interest on the two overdue quarters,
    less 30.00 discount on each of the two on-time quarters;
  - the property was paid in full from the Payments tab (1,194.00, change
    806.00);
  - the statement showed paid 1,200.00, outstanding 0.00 and due 0.00, with
    no console errors.

  An earlier attempt of that script, which failed on a missing ownership
  type, left one extra DEMO property (PIN `DEMO-E2E-…`, no RPU) in the dev
  database.
- Full suite: 136 domain, 37 application and 207 integration tests pass.

## 11. Phase 9 exit review (2026-09-26)

Roadmap exit criteria:

| Criterion | Status | Evidence |
|---|---|---|
| CLAUDE.md §74 flow CREATE PROPERTY → … → PAY → VERIFY BALANCE passes | Met | `EndToEndFlowTests`, plus the UI run above |
| Overpayment, partial and reversal (§75) covered by tests | Met | Over-tendering gives change, and money is never applied beyond what is owed (`CollectionCalculatorTests`, `CollectionFlowTests`). Partial: calculator, flow and E2E. Reversal, void and correction: `CollectionFlowTests` |
| Posting atomic under concurrent submission | Met | `ConcurrentPosting_OfOneInstallment_SavesExactlyOnePayment`: 4 simultaneous posts save one payment; the same idempotency key yields one payment |

CLAUDE.md §40 (payment) and §41 (collection):

| Item | Status |
|---|---|
| Full, partial, multiple-year, multiple tax components | Done |
| Advance payment | Done: calculator unit tests (advance discount and category); in the service it needs a posted bill for that year. No integration test pays a future year |
| Reversal, void, correction; duplicate submission protection | Done |
| Daily, cashier, payment summary, tax-type and tax-year collection | Done (Collection page receipts by date; summary by date, cashier, mode, tax type, tax year, year category, fund, account, barangay) |
| Property collection | Done (Payments tab, Statement of Account) |
| Taxpayer collection | **Gap:** no summary by payor or taxpayer yet; one more `CollectionGroupBy` value would add it |
| Collection reconciliation | Done |

Still open, carried forward:
- **DOMAIN VERIFICATION REQUIRED:**
  - the charge rules of §9 and §10 (charges follow principal; no discount
    on part of an installment; allocation order; the fixed penalty once per
    installment and tax type);
  - a correction dated like the original;
  - the void/reversal cut-off;
  - the official receipt and RCD layouts;
  - the BLGF eOR numbering format;
  - the LGU chart of accounts.
- Out of scope (§8): payment under protest, refunds and credits,
  compromise/levy/auction, EPCS/online channels, real role gating
  (Phase 12), PDF/Excel exports (Phase 11).
- Supabase lacks the migrations `Payments`, `PaymentCancellations` and
  `Remittances`.

**Phase 9 is complete with DEMO values**, apart from the taxpayer-collection
gap above.
