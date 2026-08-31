# Legacy Orders Lifecycle Map

Status: Phase 0 working document  
Captured: 2026-09-01  
Method: static source inspection only

This document records behavior that the new backend must characterize before replacing legacy order commands. It is not yet the final status-transition specification.

No database connection or runtime request was used.

## 1. Risk summary

`OrderController` contains more than 24,000 lines and combines:

- request parsing and role checks;
- validation and business policies;
- order and warehouse persistence;
- status history and audit persistence;
- file upload;
- email;
- WhatsApp automation;
- courier ownership rules;
- SignalR publication;
- background work;
- reporting and operational notifications.

The rewrite must split these responsibilities by use case without changing their observable behavior.

## 2. Initial command slices

| New slice candidate | Representative legacy action |
|---|---|
| Create order | `OrderController.Create` |
| Edit order | `OrderController.Edit` |
| Update one status | `OrderController.UpdateStatus` |
| Update statuses in bulk | `OrderController.UpdateStatusForMultiple` |
| Advance failure stage | `OrderController.AdvanceFailureStatus` |
| Mark prepared | `OrderController.MarkAsPrepared` |
| Confirm bank transfer | `OrderController.ConfirmBankTransfer` |
| Flag transfer not received | `OrderController.FlagBankTransferNotReceived` |
| Reject bank transfer | `OrderController.RejectBankTransfer` |
| Approve bank transfer | `OrderController.ApproveBankTransfer` |
| Change payment evidence | `OrderController.SetIsPaid` |
| Transfer fulfillment ownership | `OrderController.TransferOrderWarehouse` |
| Add/update failure evidence | currently shares `UpdateStatus` behavior |

These are separate use cases even when the legacy implementation shares one controller method.

## 3. Create order behavior

### Authorization

Allowed legacy roles:

- Admin;
- CallCenter;
- FollowUpDepartment;
- ExecutiveDirector.

The legacy action also uses duplicate-request prevention with a 30-second window.

### Required and normalized input

- City/state, customer name, address, store, and delivery company are required.
- Pricing category is read from form keys and must normalize successfully.
- Primary and optional secondary phone numbers are country-normalized.
- Chat URL is required for every source except WhatsApp.
- Turkey rejects Arabic address characters.
- An order screenshot is mandatory.
- Payment type is inferred from payment-receipt evidence; client-supplied `IsPaid` is not trusted.
- Customer delivery price cannot be negative.
- Delivery company/representative must be visible and match country and, for representatives, city.
- The selected delivery company must allow the inferred cash/bank-transfer payment method.

### Status derivation

- A created date more than 48 hours in the future produces a postponed order.
- A nearer future date produces a new order.
- A Turkey address shorter than 15 characters has priority and produces an incomplete order.
- Same-phone, same-store active duplicates are rejected.
- Closed statuses that permit a new order include delivered, balance-updated, and paid.
- Same-phone, same-country, different-store orders on the same day are ranked by total price.
- The highest-priced same-day similar order remains active and lower-priced ones are postponed.
- On equal price, the older order wins and the new order is postponed.
- System postponement writes a status-history reason: `AutoPostponedSimilarOrderSameDayBySystem`.

### Fraud and price rules

- Seven-or-more digit sequences are extracted from free-text fields and the secondary-phone field.
- Repeating the submitted primary phone in another field is blocked and audited.
- A suffix matching an active order in the same store is blocked and audited.
- Country/store minimum-offer rules are validated.
- Product minimum-selling-price rules are validated.
- Campaign selection must explicitly choose a valid campaign or `none`.

### Warehouse rules

- At least one positive warehouse quantity is required.
- Warehouse identifiers and amounts are validated.
- Non-exempt roles must include a product from the store's linked main warehouse.
- Selected quantities are deducted and `OrderWarehouse` rows are added.

### Persistence and side effects

Observed sequence:

1. Upload order image and receipt concurrently.
2. Insert the order and save to obtain its identity.
3. Potentially correct/persist the system-postponed status.
4. Deduct warehouse quantities and insert order-warehouse rows.
5. Insert initial status history and optional bank-transfer-pending history.
6. Save again.
7. Save pricing selection separately.
8. Queue post-commit realtime/potential-order cleanup/email work with `Task.Run`.
9. Queue WhatsApp automation with another `Task.Run`.

There is no explicit transaction around the full observed sequence. A failure after the first order save can leave a partially completed order. The new command requires one business transaction for SQL state and an outbox record, while S3 upload compensation/reconciliation handles cross-resource failure.

## 4. Edit order behavior

Observed responsibilities include:

- route ID must equal body ID;
- chat URL rule;
- non-negative customer delivery price;
- at least one existing positive warehouse selection;
- product minimum-price validation;
- phone normalization;
- audit source and price-change reason;
- order scalar updates;
- inactive-delivery-company automatic reassignment;
- order-image replacement/retention;
- receipt replacement/removal and inferred payment state;
- bank-transfer-pending history and email;
- complete warehouse edit history and quantity reconciliation;
- realtime publication.

The new API should split general order editing from payment-evidence changes and fulfillment-line changes when their authorization or transaction rules differ.

## 5. Single status update behavior

### Authorization and ownership

Allowed action roles include Admin, FollowUpDepartment, ExecutiveDirector, DeliveryCompany, CallCenter, DeliveryRepresentative, and OrderPreparer.

Additional runtime policies include:

- packaging workflow allows only `new -> prepared -> out for delivery` for assigned Turkey delivery companies;
- delivery users may change only orders assigned to their account;
- delivery-role transition policy is checked unless continuing a permitted failure flow;
- orders linked to an automated courier are blocked from manual status changes;
- CallCenter can apply processed only from an explicit set of failure/reference-processing states.

### Failure and evidence rules

- Manual failure-reason placeholders are replaced from submitted form data.
- Receipt-postponement failure reasons keep failure validation but store the final status as postponed.
- Receipt postponement changes the order date by the legacy four-day policy.
- Some failure orders require an image based on order context.
- Some target statuses always require an image.
- Legal failure stages require a reason.
- Missing reasons may be carried forward from the latest history record.
- Submitted reasons must be in the allowed reason set.
- Updating the same status may update/create its latest reason or append evidence rather than create a new transition.

### Other state rules

- Leaving the paid status clears `IsPaid`.
- Balance update is rejected unless delivered history exists.
- Processed records the employee and fixed date.
- Inactive delivery companies may be reassigned automatically during a transition.
- Every real transition adds status history with user, reason, and evidence URL.

### Ordering concern

The legacy method publishes some failure/status SignalR events before `SaveChangesAsync`. It then saves SQL state and performs employee alerts, WhatsApp automation, packaging notifications, selection deactivation, finalization events, and general realtime refresh.

The new implementation must publish durable events only after the SQL transaction commits. Realtime delivery is driven from the outbox so clients cannot observe a status that later fails to save.

## 6. Bulk status update behavior

### Selection ownership

- IDs are normalized and deduplicated.
- If any order is linked to an automated courier, the entire batch is rejected.
- Persistent `OrderStatusUpdateSelection` rows preserve selections across pagination.
- Explicit request IDs are retained as a race-safe fallback while the selection AJAX request is still in flight.
- Orders reserved by another employee are rejected.
- User-owned server selections and unreserved explicit IDs are merged.
- Expired selections are excluded.

### Business behavior

- Packaging batches enforce assigned Turkey delivery companies and the packaging transition chain.
- Per-order previous status, final status, and failure reason are tracked.
- Per-order failure reasons can come from the batch payload or saved selections.
- Automatic courier ownership and delivery-role restrictions remain applicable.
- Partial missing-reason behavior exists and must be captured exactly before replacement.
- Post-save work includes owner alerts, WhatsApp automation, packaging achievements, batch history, selection deactivation, finalization, and realtime refresh.

The new command needs an explicit atomicity decision documented in its contract: reject all, update valid orders and report per-order results, or preserve the precise legacy partial-success behavior. This cannot be inferred from a generic bulk-handler abstraction.

## 7. Mark prepared behavior

The legacy flow:

1. adds status histories;
2. saves them;
3. creates and saves an order report;
4. creates report-order links and saves them;
5. performs a direct bulk status update;
6. sends per-owner alerts;
7. publishes SignalR events.

Multiple saves and a direct bulk update occur without an observed transaction covering the whole operation. The new slice must commit histories, report links, statuses, and outbox messages atomically.

## 8. Side-effect inventory

| Trigger | Side effect |
|---|---|
| Order created | home realtime event |
| Order created | delivery/store group notifications |
| Order created | matching potential-order cleanup |
| Paid order created | bank-transfer notification email |
| Order created | WhatsApp new-order automation |
| Status changed | status SignalR events to several groups |
| Status changed | owner delivery-result alert for selected states |
| Status changed | WhatsApp status automation |
| Prepared to out-for-delivery | packaging achievement notification and invoice/email workflow |
| Bulk status completed | batch audit history |
| Bulk status completed | selection release and finalization event |
| Failure | failed-order notification and evidence/history writes |

Every durable side effect becomes an outbox message with an idempotency key. UI refresh events can be coalesced when safe, while employee/business notifications remain individually auditable.

## 9. Performance targets for the new order module

- One bounded query plan for each validation group; avoid repeated entity reloads.
- Project read data instead of loading broad graphs where mutation is not required.
- Preserve parallel S3 uploads without sharing an EF `DbContext` across tasks.
- Keep provider calls and email outside SQL transactions and request latency.
- Batch history and warehouse-line inserts.
- Use optimistic concurrency for inventory and order state.
- Replace per-order network work in bulk handlers with outbox batches.
- Publish realtime after commit.
- Measure query count, SQL duration, allocation, p95, and p99 for create/edit/status commands.

## 10. Required characterization suite

Before implementation, create table-driven cases for:

- every role and ownership combination;
- packaging workflow transitions;
- automated courier-linked orders;
- failure-stage progression and reason carry-forward;
- required evidence rules;
- receipt postponement;
- paid/balance-update interactions;
- same-day similar-order ranking and equal-price tie behavior;
- duplicate and fraud phone rules;
- delivery-company country/city/payment compatibility;
- warehouse quantity concurrency;
- selection ownership across pagination;
- bulk partial/missing-reason behavior;
- side-effect idempotency and retry.

## 11. Open questions

- Is legacy bulk missing-reason behavior intentionally partial or a defect to correct in a versioned contract?
- Which status updates must be synchronous from the user's perspective beyond the SQL commit?
- Can existing web clients accept `409 Conflict` for stale/concurrent status changes?
- Which legacy SignalR event names must remain temporarily compatible?
- Is order creation allowed to succeed when pricing-selection persistence fails?
- Should S3 uploads happen before validation that currently occurs later in the command?
- Which status rules are country/store-specific but not yet centralized?

