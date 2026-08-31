# Legacy Integration Catalog

Status: Initial static catalog  
Captured: 2026-09-01

## Provider matrix

| Integration | Legacy responsibility | New boundary | Required reliability controls |
|---|---|---|---|
| CAMEX | Cities, stores, shipment creation, webhook state, retry, reconciliation | `ICourierGateway` plus CAMEX adapter | timeout, idempotency, webhook validation, retry, reconciliation |
| Sandoog | Order submission, status mapping, webhook, retry | `ICourierGateway` plus Sandoog adapter | timeout, idempotency, webhook validation, retry, reconciliation |
| Infobip SMS | Transactional SMS | `ISmsSender` | bounded retries, provider message ID, delivery audit |
| Infobip WhatsApp | WhatsApp messages | `IWhatsAppSender` | idempotency, template validation, delivery audit |
| WhatsApp automation | Event-driven order messaging | notification policy plus outbox consumer | deduplication, account routing, retry, dead-letter |
| Facebook | Incoming webhook and bot behavior | signed webhook endpoint and application handler | signature validation, replay protection, idempotency |
| AWS S3 | Durable files and media index | `IObjectStorage` | upload/SQL reconciliation, checksum, cleanup, presigned URLs |
| AWS CloudWatch | Infrastructure metrics | telemetry exporter/adapter | batching, failure isolation, redaction |
| Cloudflare | Cache purge and publish detection | `IEdgeCacheInvalidator` | bounded purge, audit, retry, environment guard |
| SMTP | Operational email and invoices | `IEmailSender` through outbox | idempotency, retry, attachment limits, audit |
| SignalR | Orders, messages, conferences, edit presence, script editing | realtime publisher and hub endpoints | event contracts, authorization, Redis backplane for scale-out |
| PDF stack | Invoice/report generation and printing | worker-side document renderer | process isolation, timeout, golden-master tests, native dependency health |
| Voice transcription | Voice search input | `IVoiceTranscriber` | upload limits, timeout, sanitized provider failure |
| Image analysis | Search/vision behavior | `IImageAnalyzer` | decode limits, cancellation, deterministic error mapping |
| Flutter | Mobile authentication and mixed MVC/API consumption | explicit versioned REST API | DTO compatibility, stable status codes, token lifecycle |

## Cross-cutting integration rules

1. No provider SDK object escapes its infrastructure adapter.
2. Provider responses are mapped into stable application results.
3. No avoidable provider call holds an SQL transaction open.
4. Side effects initiated by committed business state use an outbox.
5. Every retryable operation has an idempotency strategy.
6. Every webhook has authentication, replay protection, and deduplication.
7. Every adapter exposes latency, success, failure, timeout, and retry metrics.
8. Credentials are validated at startup and never written to logs.
9. Test implementations simulate slow, duplicate, malformed, and unavailable providers.
10. Reconciliation exists when a lost callback can leave business state stuck.

## Open integration questions

- Which provider operations already expose a usable idempotency key?
- Which webhooks supply stable event identifiers?
- Which integrations are required synchronously for the user-visible response?
- What are the current provider rate limits and timeout expectations?
- Which SignalR event names are consumed by Flutter versus Razor pages?
- Which PDF outputs require byte-level, visual, or data-only compatibility?
- Where will Redis be hosted relative to the IIS application?

