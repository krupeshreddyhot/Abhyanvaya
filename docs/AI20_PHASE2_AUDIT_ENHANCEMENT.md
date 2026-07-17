# AI20.PHASE2.0.6 — Audit Architecture Enhancement

**Type:** Contract design review only. This document evaluates the proposed `IEnrollmentAuditService` from `AI20_PHASE2_ENGINE_CONTRACTS.md` §12. It does not implement or amend that contract.

**Decision:** Keep one `RecordAsync(EnrollmentAuditEvent, CancellationToken)` entry point for Phase 2. Classify each action deterministically as `Business` or `Diagnostic` for routing and retention, but do not add three caller-selected methods. Reserve `Security` for genuine security events owned by a platform-wide security-audit capability; the enrollment module does not currently have enough such events to justify `AuditSecurity`.

---

## 0. Scope and reviewed context

This review is grounded in:

- `AI20_PHASE2_ENGINE_CONTRACTS.md` §12, where the proposed contract contains one `RecordAsync` method, nine `EnrollmentAuditAction` values, and one structured `EnrollmentAuditEvent`.
- `AI20_ENROLLMENT_BACKGROUND.md` §3.7, which establishes structured operational logs containing batch, student, job, status, failure, duration, and recognition-style correlation identifiers.
- `ICurrentUserService`, which exposes the authenticated application's numeric `UserId`, `Role`, and `TenantId` (plus staff/course/group context). It is the relevant trusted source for human actor identity, although the proposed command DTOs also carry `RequestedByUserId`.

This review distinguishes three concerns that the current contract describes collectively as “audit”:

1. **Business audit:** durable evidence of human administrative actions and meaningful batch milestones.
2. **Diagnostic telemetry:** high-volume details used to operate and troubleshoot the pipeline.
3. **Security audit:** evidence of authentication/authorization or suspicious behavior.

Those concerns can require different storage and retention without requiring three public methods.

---

## 1. Classification of the current action set

The current nine-value enum already provides enough information to classify every Phase 2 event without asking each caller to choose a method.

| `EnrollmentAuditAction` | Recommended category | Rationale |
|---|---|---|
| `BatchCreated` | Business | Human-initiated creation of a bulk administrative operation. |
| `BatchCancelled` | Business | Human-initiated lifecycle decision with material effect. |
| `BatchResumed` | Business | Human-initiated restart of previously cancelled work. |
| `BatchCompleted` | Business | Meaningful system-generated business milestone; retain summary and outcome. |
| `ItemTransition` | Diagnostic | Routine, high-volume pipeline state movement already suited to structured logs. |
| `ItemFailed` | Diagnostic | Per-item operational outcome; aggregate or promote only when policy thresholds are crossed. |
| `ItemCompleted` | Diagnostic | Routine per-item completion; the durable item state remains the system of record. |
| `BulkRetryRequested` | Business | Human-initiated reprocessing decision that should identify actor, scope, and result. |
| `StuckItemReset` | Diagnostic | Autonomous recovery action unless a human explicitly requested it. |

No existing value is intrinsically a security event. SuperAdmin-only access is an authorization policy, not by itself an enrollment security event. A denied access attempt, role-policy mismatch, token anomaly, cross-tenant probe, or suspicious request pattern would be security-relevant, but those are API/authentication concerns and should be captured consistently across the platform rather than invented only for enrollment.

If a human can manually reset one stuck item in a later phase, that action should either become a distinct business action or carry an initiation mode; category must not depend on guessing from a nullable actor alone.

---

## 2. Three-method split: benefits and drawbacks

The proposed alternative would expose methods equivalent to `AuditBusinessEvent`, `AuditDiagnostic`, and `AuditSecurity` (using the repository's async convention, actual asynchronous methods would normally have an `Async` suffix).

| Consideration | Benefit of three methods | Drawback or limit in Phase 2 |
|---|---|---|
| Retention | Makes indefinite/long-term business retention and 30/90-day diagnostic rotation explicit. | Retention belongs to sink policy and event classification; method choice alone does not enforce deletion or preservation. |
| Backing stores | Business/security can route to a durable append-only table or SIEM while diagnostics go only to structured logs. | One method can route identically from a category derived from `Action`, with less caller surface. |
| Consumers | Compliance export can consume Business + Security and omit Diagnostic noise. | Consumers can filter a stable category field; three methods do not remove the need for category metadata in exported records. |
| Call-site intent | A method name makes developer intent visible during review. | Developers can choose the wrong method. The same action may then be retained differently across call sites. |
| Validation | Each method could require category-specific fields, such as an actor for an admin command. | Three methods taking the same generic event do not provide compile-time validation; distinct DTOs would be needed, increasing the contract further. |
| Operational isolation | Diagnostic sink failures and durable audit failures could have different delivery behavior. | This is an implementation/routing concern. It can be implemented behind one interface entry point. |
| Security visibility | A dedicated method advertises that security events matter. | There are no current enrollment-specific security actions. A method with no credible callers creates speculative architecture and may fragment platform security auditing. |
| Simplicity | Separate paths can be intuitive once policies are mature and substantially different. | Phase 2 has only nine actions and a relatively small contract catalog. Three methods add choices without adding information that the enum does not already supply. |
| Evolution | Category-specific methods can evolve toward distinct schemas. | They make future changes harder if event classification moves or if one event must feed multiple sinks/categories. |
| Testing | Tests can verify callers selected the intended path. | Central action-to-category mapping is easier to exhaustively test: every enum value must map exactly once. |

The strongest reasons to split are differentiated delivery guarantees and genuinely different required schemas. Neither is yet defined for Phase 2. Different retention, stores, and consumers alone justify a category and routing policy, not necessarily separate caller methods.

---

## 3. Recommended contract direction

Retain the proposed single method:

```csharp
// Illustrative proposal only; no C# file was created or modified.
Task RecordAsync(
    EnrollmentAuditEvent auditEvent,
    CancellationToken cancellationToken = default);
```

Add category semantics to the design before implementation, preferably through a centralized, exhaustive mapping from `EnrollmentAuditAction` rather than a caller-settable value:

```csharp
// Illustrative proposal only.
public enum EnrollmentAuditCategory
{
    Business,
    Diagnostic
}

// Conceptual mapping owned beside the audit adapter:
// BatchCreated/Cancelled/Resumed/Completed/BulkRetryRequested => Business
// ItemTransition/ItemFailed/ItemCompleted/StuckItemReset      => Diagnostic
```

An exporter should emit the computed category as a field such as `audit.category`. It should not trust arbitrary callers to supply a category that controls retention. An exhaustive switch makes addition of a new action a compile-time-visible policy decision.

`Security` should not be added merely to make the enum appear enterprise-complete. If enrollment later owns a real security action, add the category then and decide whether the event belongs in a shared platform `ISecurityAuditService`. Security events commonly need request/network/authentication context that the enrollment event does not have.

The current description also needs one conceptual correction before implementation: calling an event stream “tamper-evident” is not enough to make it so. `RecordAsync` plus an ordinary structured-log sink provides append-style behavior, but no cryptographic chaining, write-once storage, restricted mutation, or independent delivery guarantee.

---

## 4. Enterprise audit requirements gap analysis

Azure Activity Log, AWS CloudTrail, and SOC 2-oriented audit trails vary in implementation, but converge on durable identity, time, action, resource, result, integrity, retention, and export controls.

| Requirement | Current design status | Recommendation |
|---|---|---|
| Event occurrence timestamp in UTC | **Missing.** The event has no timestamp and therefore relies on sink ingestion time. | Add an explicit immutable `OccurredUtc` generated by trusted application/infrastructure code; also preserve sink `IngestedUtc` where available. |
| Unique event identifier | **Missing.** `ExecutionTraceId` correlates work but is not a unique event ID. | Add `EventId` (GUID/UUID) for deduplication, replay, and export acknowledgements. |
| Authenticated actor identity | **Partial.** `ActorUserId` identifies a human numerically; `ICurrentUserService` can supply user, role, and tenant. System actions use null. | Capture actor from trusted `ICurrentUserService`/claims at the API boundary, not an unverified DTO field alone. Add `ActorType` (`User`, `System`, `Service`) and optionally role/subject identifier. |
| Tenant context | **Satisfied for basic scoping.** `TenantId` is required. | Preserve it as a first-class field and verify it against trusted tenant context for human actions. |
| Action vocabulary | **Satisfied for current scope.** Nine enum values are stable and specific. | Keep stable machine-readable action names and version their semantics; derive category centrally. |
| Target/resource identity | **Partial.** Batch, item, and student IDs exist, but target type and canonical resource are implicit. | Add `TargetType` plus a canonical `TargetId`/`ResourceId`; retain typed IDs as useful domain attributes. |
| Outcome/result | **Missing.** `Action` records what was attempted/observed but not success, denial, no-op, or failure. | Add `Outcome` (`Succeeded`, `Failed`, `Denied`, `NoOp`) and a bounded non-sensitive `ReasonCode`; do not infer outcome from `FailureCategory`. |
| Before/after values | **Partial.** `FromStatus` and `ToStatus` cover item status only. Batch changes, retry filters/counts, and cancellation metadata are not represented. | Use bounded typed change fields or a redacted property-change structure for business events. Avoid unconstrained object snapshots and sensitive data. |
| Correlation across request and pipeline | **Partial.** `ExecutionTraceId` correlates one item execution. It does not explicitly cover the originating HTTP request, batch command, distributed trace, or span. | Define `CorrelationId`/`PipelineExecutionId` semantics and optionally add OTel `TraceId` and `SpanId`; do not overload one GUID with several meanings. |
| Source/request context | **Missing.** No service name, environment, request ID, client IP, user agent, or authentication mechanism. | Add only where justified: `SourceService`, `Environment`, `RequestId`, and for security events separately controlled network/auth context. Treat IP/user-agent data under privacy policy. |
| Immutability and tamper evidence | **Missing.** The contract says append-only/tamper-evident, but supplies no enforcement. | For compliance-grade business audit, use append-only permissions, deletion controls, immutable/WORM retention where required, and optionally hash chaining/signatures with monitored verification. |
| Non-repudiation | **Missing.** A caller can construct actor/time/detail values; no signature or trusted identity binding is defined. | Bind actor and time in trusted infrastructure. If legal non-repudiation is required, add signed records/key management and independent evidence; ordinary application logs are not non-repudiation. |
| Durable delivery | **Missing/contradictory.** Sink errors are swallowed and per-item logs may be fire-and-forget, so events can be lost silently. | Diagnostic loss may be acceptable and measurable. Business audit should use acknowledged delivery, a transactional outbox, or reconciliation against domain state; alert on audit-delivery failure. |
| Ordering | **Partial.** Status pairs and trace IDs help, but there is no event time or sequence. Concurrent events can reorder. | Add timestamp and, only if required, an aggregate sequence/version for a batch or item. Do not promise global ordering. |
| Retention and legal hold | **Missing.** A future audit table is permitted, but no policy is defined. | Configure category-specific retention: long-lived business records per institutional/legal policy; shorter diagnostic rotation (for example 30/90 days); security retention per platform policy. Support legal hold if required. |
| Access control and segregation of duties | **Missing.** Writer/read/export permissions are unspecified. | Restrict append, query, export, and deletion separately; ensure application operators cannot silently alter compliance records. Audit access to the audit store itself. |
| Export and portability | **Missing.** No schema version or export contract exists. | Add `SchemaVersion`, stable names, UTC timestamps, and documented serialization. Provide checkpointed export only when a SIEM/compliance consumer exists. |
| Sensitive-data minimization | **Partial.** Image bytes, vectors, secret URLs, and sensitive detail are explicitly prohibited. Free-text `Detail` can still leak data. | Prefer reason codes and allow-listed structured attributes; cap/sanitize `Detail`; classify student identifiers and define access/retention controls. |
| Availability monitoring | **Missing.** “Swallow and log” can conceal audit loss. | Emit sink health metrics, dropped-event counters, alerts, and reconciliation reports appropriate to category. |

For Phase 2, the highest-value schema gaps are `EventId`, `OccurredUtc`, `ActorType`, `Outcome`, explicit target identity, and clear correlation semantics. Full cryptographic non-repudiation or WORM storage should be driven by an actual regulatory or contractual requirement, not assumed.

---

## 5. Actor identity and trust boundary

`ICurrentUserService` is more trustworthy than accepting `RequestedByUserId` as the sole evidence of who acted because it is populated from the authenticated request context. The audit design should:

1. Resolve the actor at the API/application boundary from `ICurrentUserService.UserId`, `Role`, and `TenantId`.
2. Verify request tenant scope against the trusted tenant context.
3. Pass an immutable actor context into work that continues after the request, rather than resolving `ICurrentUserService` inside a background scope with no user.
4. Represent autonomous work explicitly as `ActorType = System` or `Service` with a stable service identity; do not treat `ActorUserId = null` as complete actor evidence.
5. Avoid copying mutable fields such as `CourseId` or `GroupId` unless they are relevant to the audited authorization decision.

The command contract may continue carrying a requester ID for orchestration, but an audit implementation should not claim non-repudiation unless the value was bound to authenticated claims by trusted code.

---

## 6. SIEM compatibility

A SIEM export needs a stable, versioned envelope and predictable field names. CEF is one possible transport representation, but a vendor-neutral JSON schema with documented mapping is sufficient initially. CEF conversion can happen at the exporter; the Application contract should not depend on Splunk, Sentinel, or CEF types.

| Common/SIEM field | Current source | Compatibility | Recommendation |
|---|---|---|---|
| `event.id` | None | Missing | Add unique `EventId`. |
| `event.schema_version` | None | Missing | Add `SchemaVersion` for safe evolution. |
| `event.timestamp` / CEF receipt time | Sink timestamp only | Partial | Add `OccurredUtc`; exporter may also include ingestion time. |
| `event.category` | Implied by `Action` | Partial | Export computed `Business`/`Diagnostic`; add `Security` only when real events exist. |
| `event.action` | `Action` | Good | Serialize stable names, not only numeric enum values. |
| `event.outcome` | None | Missing | Add `Outcome`; use standardized values. |
| `actor.id` / CEF `suser` | `ActorUserId` | Partial | Add `ActorType`; optionally include trusted role and external subject ID. Use a stable system/service actor when no human exists. |
| `tenant.id` | `TenantId` | Good | Keep required and consistently named. |
| `target.type` | Implied by populated IDs | Missing | Add explicit `TargetType`. |
| `target.id` / CEF `dvc` or extension field | `BatchId`, `ItemId`, `StudentId` | Partial | Emit a canonical resource ID plus typed domain IDs as extension fields. |
| `source.service` | None | Missing | Add/export service name and deployment environment from trusted resource attributes. |
| `source.ip` / CEF `src` | None | Missing for security use | Capture only at the HTTP/security boundary when justified; not needed for worker diagnostics. |
| `status.from`, `status.to` | `FromStatus`, `ToStatus` | Good for item transitions | Keep as structured attributes and omit when inapplicable. |
| `error.category` | `FailureCategory` | Good | Keep machine-readable; add a bounded reason code if failure categories are insufficient. |
| `trace.id` | `ExecutionTraceId` | Partial | Preserve as pipeline execution correlation; separately map actual OTel trace ID if adopted. |
| `span.id` | None | Missing | Add only with OTel instrumentation or obtain it from ambient activity during export. |
| `message` | `Detail` | Partial | Keep optional, bounded, sanitized, and secondary to structured fields. |

Recommended vendor-neutral serialized names include:

- `event.id`, `event.schema_version`, `event.occurred_utc`, `event.category`, `event.action`, `event.outcome`
- `tenant.id`, `actor.type`, `actor.id`, `actor.role`
- `target.type`, `target.id`, `enrollment.batch_id`, `enrollment.item_id`, `student.id`
- `status.from`, `status.to`, `error.category`, `reason.code`
- `correlation.id`, `pipeline.execution_id`, `trace.id`, `span.id`
- `service.name`, `deployment.environment`, `message`

Business and future security events should be eligible for reliable, checkpointed SIEM export. Diagnostic events should normally remain in the structured logging pipeline and be exported only if operational needs justify the volume and cost.

---

## 7. OpenTelemetry compatibility

The current event maps naturally to an OpenTelemetry Log Record. The action can form the event name/body, while domain values are attributes:

| Current field | Suggested OTel log attribute | Assessment |
|---|---|---|
| `Action` | `event.name` or `audit.action` | Clean mapping; export a stable string. |
| `TenantId` | `tenant.id` | Clean mapping. |
| `BatchId` | `enrollment.batch.id` | Clean mapping when present. |
| `ItemId` | `enrollment.item.id` | Clean mapping when present. |
| `StudentId` | `student.id` | Clean mapping, subject to privacy controls. |
| `ActorUserId` | `enduser.id` or `audit.actor.id` | Partial; null does not distinguish system/service actors. |
| `FromStatus` | `audit.status.from` | Clean mapping when present. |
| `ToStatus` | `audit.status.to` | Clean mapping when present. |
| `FailureCategory` | `error.type` or `enrollment.failure.category` | Clean mapping; avoid claiming an exception when there is none. |
| `ExecutionTraceId` | `pipeline.execution.id` | Clean as a custom correlation attribute, but it is not automatically an OTel `TraceId`. |
| `Detail` | Log body or `audit.detail` | Usable if bounded and sanitized; structured attributes should carry queryable facts. |

The missing OTel-compatible envelope data is:

- `OccurredUtc` for the Log Record timestamp.
- Severity/severity number. Business successes are normally informational; failures/recovery warnings can be warning/error according to operational policy. Category must not be inferred from severity.
- `EventId`, `AuditCategory`, `Outcome`, `ActorType`, `TargetType`, and schema version as attributes.
- Resource attributes such as `service.name`, `service.version`, `deployment.environment.name`, and instance identity, normally supplied by OTel resource configuration rather than the domain event.
- A real 16-byte OTel `TraceId` and 8-byte `SpanId` from the ambient `Activity` when the event occurs within an instrumented trace.

`ExecutionTraceId` should remain a pipeline/business correlation value unless Phase 2 explicitly defines it as an OTel trace identifier. A GUID is not automatically equivalent to OTel trace context, and background processing often crosses a queue/database boundary where parent context must be propagated deliberately.

Sibling Phase 2 milestones are expected to introduce concepts such as `PipelineExecutionId` and `CorrelationId`. The eventual shape should keep their meanings distinct:

- `CorrelationId`: follows the originating request or business operation across components.
- `PipelineExecutionId`: identifies one enrollment processing attempt/execution.
- OTel `TraceId`: identifies one distributed trace and may span multiple operations.
- OTel `SpanId`: identifies the current operation inside that trace.

These identifiers may be linked, but should not be renamed interchangeably. A future exporter can map `PipelineExecutionId`/`CorrelationId` to custom log attributes while the OTel SDK supplies trace and span context from `Activity.Current`.

The audit event should be emitted as an OTel Log Record for retention/search. Selected business milestones may also be added as span events for trace diagnosis, but spans are sampled and expire under observability retention; they must not be the sole compliance audit store.

---

## 8. Delivery, retention, and backing-store policy

One entry point can support category-specific policy:

| Category | Initial Phase 2 destination | Delivery expectation | Illustrative retention | Typical consumers |
|---|---|---|---|---|
| Business | Structured log initially; durable append-only store when compliance requires it | Detect and report loss; durable/acknowledged delivery once treated as compliance evidence | Institutional/legal policy; potentially multi-year or indefinite | Administrators, auditors, compliance export, SIEM |
| Diagnostic | Existing structured logging pipeline | Best effort with dropped-event metrics acceptable | Operational window such as 30 or 90 days | Support, engineering, operations |
| Security (future/platform) | Central security log/SIEM | Reliable, access-controlled, alertable | Organization security policy | Security operations, incident response, auditors |

Retention durations above are examples, not requirements. The owning institution must set them based on legal, contractual, privacy, and cost constraints.

The present “audit must never fail the primary operation” principle is reasonable for pipeline availability, but it must not silently redefine a lossy log as a compliance trail. A future durable business-audit design should prefer an outbox/reconciliation mechanism so the primary operation can succeed while audit delivery is retried and observable. Whether the outbox write shares the business transaction is an implementation decision for that later milestone.

---

## 9. Decision triggers for revisiting the interface

Revisit the one-method decision when at least one of these becomes concrete:

1. A regulatory, contractual, or institutional policy mandates a durable audit record with specified retention, legal hold, or non-repudiation.
2. A real Splunk, Microsoft Sentinel, or other SIEM integration defines a schema, throughput, and delivery SLA.
3. Enrollment gains genuine security events that are not already owned by platform authentication/authorization auditing.
4. Business, diagnostic, and security records require materially different mandatory DTO fields that cannot be validated behind one event envelope.
5. The action vocabulary grows enough that one centralized mapping becomes ambiguous, especially when the same action can belong to different categories based on initiation context.
6. Audit loss must be reconciled transactionally rather than observed through sink health.

At that point, prefer separate typed event records or category-specific writers over three methods that all accept the same generic DTO. Different schemas and guarantees are a stronger architectural boundary than different method names alone.

---

## 10. Final recommendation

**Keep `IEnrollmentAuditService.RecordAsync` as the single Phase 2 entry point.** Do not split it into `AuditBusinessEvent`, `AuditDiagnostic`, and `AuditSecurity` now.

The current nine actions divide deterministically into five Business and four Diagnostic actions. A centralized computed `AuditCategory` gives retention, routing, and consumer filtering without requiring every caller to make a policy choice. It also keeps the relatively small Phase 2 contract surface focused and leaves implementations free to route categories to different stores.

Do not introduce an enrollment-specific Security method or category until a genuine enrollment security event exists. Authorization decisions and suspicious request patterns should preferably flow through a platform-wide security audit capability with request/authentication context.

Before implementation, strengthen the event-envelope design with explicit occurrence time, unique event ID, actor type, outcome, target identity, schema version, and clarified correlation identifiers. Treat `ActorUserId` as trustworthy only when captured from authenticated context such as `ICurrentUserService`, and represent autonomous actors explicitly.

Finally, document delivery truthfully: initial structured logs are operationally useful and OTel/SIEM-friendly, but they are not automatically immutable, tamper-evident, non-repudiable, or compliance-grade. Add durable storage, monitored delivery, retention controls, and export only when a real requirement justifies them.

---

## Constraints Confirmed

- No `.cs`, `.csproj`, `.tsx`, `.ts`, or other non-markdown file was created or modified.
- No interface, DTO, enum, service, database object, endpoint, worker, DI registration, frontend component, or telemetry exporter was implemented.
- Every C# block in this document is illustrative/proposed only.
- The existing `docs/AI20_PHASE2_ENGINE_CONTRACTS.md` contract remains unchanged.
- The only artifact produced by this task is `docs/AI20_PHASE2_AUDIT_ENHANCEMENT.md`.
