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

Next: 9d (receipt form, payment workspace, Payments tab, balance on the
Statement of Account).
