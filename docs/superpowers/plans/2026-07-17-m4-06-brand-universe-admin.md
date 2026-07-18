# M4-06 Brand and Universe Admin Plan

**Goal:** Deliver complete Thai-first Brand and Universe Admin vertical slices: authorized URL-driven lists, same-page create/edit/archive flows, required local image/logo handling, structured validation/business failures, PostgreSQL concurrency safety and recoverable media cleanup.

**Architecture:** Brand and Universe remain separate Application vertical slices with one handler per action. Application declares narrow feature-specific read and mutation-session ports; Infrastructure implements them with EF Core/Npgsql and creates one fresh `ApplicationDbContext` per query or mutation. The mutation session owns its context, transaction, aggregate tracking, advisory lock, slug allocation, constraint classification and commit verification so all work for one operation shares one context without leaking EF into Application. Razor owns only presentation state and `ISender.Send(...)`.

## Locked decisions

- All visible UI, validation, status, confirmation and feedback copy is Thai-first and uses the existing borderless Muted Ocean Admin theme.
- Brand and Universe routes retain explicit `CanAccessAdmin` shell policy metadata and add the granular `CanManageProducts` policy. Every query/command independently enforces `CanManageProducts` before validation, database and storage work; menu visibility is never a security boundary.
- Create requires exactly one Brand image or Universe logo. Edit may retain or replace committed media, but a logo-less seeded Universe must select one before save. There is no remove-without-replacement action.
- `DisplayName` and `EnglishName` are independently unique across active and archived rows. Validate both trimmed persisted length and Form-KC normalized length at 200 characters. English name must produce a valid ASCII slug base.
- Slug is never browser input. Create and English-name edits reallocate it transaction-safely under the approved M4-02 domain contract, excluding the current row on edit and adding deterministic suffixes. UI renders non-clickable technical copy `ส่วน URL (slug)`; no public link, redirect or history is invented.
- Archive is terminal; there is no hard delete/restore. It is allowed while Products/Characters reference the row. Existing FKs, snapshots and media remain; archived references are excluded from future selection/publication.
- Seeded Marvel/DC/Unknown follow the same edit/archive rules as custom Universes because no approved seed-protection rule exists. Their IDs stay fixed and their initial missing-logo state is truthful.
- Default list status is Active. URL state is `q`, `status=active|archived|all`, `page`; default Active/page 1 values are omitted. Fixed UI page size is 20. Ordering is `UpdatedAtUtc DESC, Id`. Beyond-last pages clamp to the last non-empty page and replace-navigate to the canonical URL; total zero renders Empty with no pagination.
- Lists expose real reference counts only. Brand count is Products; Universe exposes Products and Characters separately.
- Brand/Universe have a persisted `long` concurrency version. Admin create persists version 1; one successful create/update/archive command advances/persists one command version only. Rejected/no-op/stale work does not advance it.
- File selection creates a browser-local `blob:` preview only. No staging/upload occurs before submit. Object URLs are revoked on replace/reset/close/dispose.
- New media commits before database save/commit. Definite rollback/save failure compensates the new key with `CancellationToken.None`; old media remains. Old media deletes only after verified durable DB success.
- A commit acknowledgement failure is not assumed rollback. Infrastructure verifies action-specific entity/version/status evidence and a full trusted-storage-key reference search using a fresh context. Confirmed/superseded commit proceeds; compensation is allowed only when the key is freshly proven unreferenced; unavailable verification retains media and records an unresolved cleanup/verification entry so a possible committed row is never broken.
- Cleanup ledger accepts only opaque trusted keys from staged media or persisted DB snapshots. Repeated recording is idempotent by unresolved storage key. Future deletion must recheck every database media reference first; no worker/scheduler is introduced.
- Authorization, structured field validation and typed business conflicts short-circuit as explicit `Result` failures, not exceptions. Unexpected database/storage/invariant faults are logged once and receive a safe Thai system message through one shared Admin request executor; cancellation is rethrown and never terminates a circuit through the executor.
- No generic repository, shared circuit DbContext for catalog operations, Category/Character management page, hard delete, unarchive, remote storage, job framework, UI framework, default browser select/file styling or second dialog module.

## Task 1: RED domain limits, atomic command versions and persistence ports

**Files:**

- Modify Brand/Universe aggregates under Domain/Catalog
- Create `CatalogReferenceLimits.cs`
- Create narrow Brand/Universe read and mutation-session contracts under Application feature folders
- Modify Infrastructure persistence registration
- Create focused Domain/architecture tests

- [x] RED locks normalized and trimmed 200-character boundaries, version 1 on Admin create, exactly one version increment per atomic Admin update/archive, and no increment after rejected/stale/no-op work.
- [x] GREEN add atomic create-with-media and update-details-with-media Domain operations while preserving invariant-only constructors and terminal archive behavior.
- [x] Define non-generic `IBrandListReader`/`IUniverseListReader` and aggregate-specific mutation-session factories justified by transaction, concurrency and media rules; do not expose EF, Npgsql, `DbSet` or `IQueryable` from Application.
- [x] Infrastructure uses `IDbContextFactory<ApplicationDbContext>` (or equivalent fresh factory) so every catalog query/mutation gets one operation context; slug allocation and transaction share the mutation context.
- [x] Keep existing scoped Identity/startup DbContext behavior intact while proving overlapping same-circuit list/list and list/mutation operations never share a change tracker.
- [x] Architecture tests reject generic repositories, EF/Npgsql/Infrastructure references in Application and DbContext access from Razor.

## Task 2: RED common Result response, authorization, validation and logging ownership

**Files:**

- Create structured field-failure model and result-request contract under Application/Common
- Create `ICurrentUserAuthorization` and AuthorizationBehavior
- Modify ValidationBehavior, LoggingBehavior and Application registration
- Create circuit-correct Web authorization implementation
- Modify GlobalExceptionHandler HTTP logging ownership
- Retrofit existing validated requests/tests

- [x] One provider-neutral request contract creates Unauthorized, Forbidden, structured Validation and persistence/business failure responses without reflection.
- [x] AuthorizationBehavior runs before ValidationBehavior, evaluates `CanManageProducts`, and writes the principal-derived actor onto the unique request invocation; browser actor/role/claim input is impossible.
- [x] Web authorizer uses `AuthenticationStateProvider` plus ASP.NET authorization for interactive circuits, not only `IHttpContextAccessor`.
- [x] ValidationBehavior converts FluentValidation failures into a Thai `Validation.Failed` Result carrying stable property paths/messages for FormValidationStore; expected validation never throws.
- [x] Retrofit Register/ChangePassword and any existing validator-backed requests so regression behavior remains explicit and Thai.
- [x] Task 2 locks the first pipeline stages as Logging → Authorization → Validation → Handler. Task 5 later inserts PersistenceErrorMapping after Validation and before any optional automatic Transaction.
- [x] Typed Result failures never produce Error logs. Unexpected MediatR exceptions are logged once and marked; the HTTP GlobalExceptionHandler logs only unmarked HTTP exceptions. Interactive conversion to Thai UI is owned by the shared Admin request executor in Task 10. Cancellation is never Error-logged.
- [x] RED proves unauthorized requests reveal no validation detail and touch neither database nor storage; overlapping request instances never leak actor state.

## Task 3: RED operation-owned transactions, commit outcomes and media coordinator

**Files:**

- Modify TransactionBehavior contract tests only as needed to prove catalog mutations opt out explicitly
- Create provider-neutral commit outcome/evidence contracts
- Create the provider-neutral cleanup-registry contract and commit-outcome system errors used by coordinator fakes
- Create media mutation coordinator under Application/Common/Files
- Implement outcomes inside Infrastructure mutation sessions
- Create fault-injection tests

- [x] Existing automatic TransactionBehavior remains available only to requests that explicitly implement its command marker. Every Brand/Universe create/update/archive request intentionally omits that marker and owns exactly one fresh mutation session, so no catalog operation enters a nested or circuit-scoped transaction.
- [x] Mutation session is once-only: no execution-strategy replay of domain/filesystem callback. Typed failure rolls back without Save and clears tracked state; exception rolls back/clears before propagation.
- [x] Stage once outside the DB transaction. Inside the operation transaction, lock/load/recheck/version, mutate, commit staged media, then save/commit DB.
- [x] Commit outcome distinguishes `Committed`, `DefinitelyRolledBack` and `Indeterminate`; do not delete new media solely because Commit threw.
- [x] After an acknowledgement failure, use a fresh context and action-specific evidence. For create/replacement, first search all persisted media references by the trusted new key: any reference confirms the file must remain even when the target version is newer; compensate only after a fresh full-reference check proves the key unreferenced. For details-only update/archive, verify ID, version >= intended version and intended details/slug or archived status; a newer superseding state returns success plus authoritative refresh. Unavailable/inconsistent verification retains media, records unresolved state and returns logged safe `Persistence.CommitOutcomeUnknown` instructing Admin to refresh before retry.
- [x] Immediately before every compensation delete, repeat the fresh full-reference guard. Tests cover commit acknowledgement loss followed by a winning archive/update before verification, including a newer row that still references the key.
- [x] Failure matrix covers stage/commit/save/rollback/commit-ack/cancellation: confirmed rollback removes new media with a non-cancelled token, preserves old media and leaves no row; confirmed commit retains new media and proceeds with old cleanup.
- [x] Coordinator lifecycle uses method-local immutable state, never plain scoped/AsyncLocal circuit mutation. Overlapping sends remain isolated.

## Task 4: RED idempotent cleanup ledger and early migration

**Files:**

- Create internal Infrastructure cleanup entity/configuration/implementation
- Add Brand/Universe version mapping
- Create Code First migration, snapshot and idempotent SQL artifact
- Create ledger/migration tests

- [x] Ledger stores opaque StorageKey, reason, entity type/id context, first-observed UTC, last-attempt UTC, attempt count and resolution UTC; enforce one unresolved row per storage key.
- [x] Accept keys only from a trusted `StagedMedia` result or persisted media snapshot. Browser-provided URLs/keys are never accepted.
- [x] Recording uses a fresh immediate transaction after the primary transaction, is idempotent, and treats an already-missing file safely.
- [x] Ledger-write failure is safely logged and never reverses an already committed mutation or invites client retry.
- [x] Future reconciliation contract requires a fresh full reference check before delete; M4-06 adds no worker or automatic deletion loop.
- [x] Migration adds only Brand/Universe bigint versions and cleanup ledger with deterministic existing/seed values. Review destructive/Identity/seed SQL and apply twice to a disposable baseline before handler PostgreSQL tests.
- [x] EF pending-model gate is clean immediately after this task.

## Task 5: RED typed errors, mutation locks and provider error mapping

**Files:**

- Create BrandErrors/UniverseErrors and common persistence failure contracts
- Adapt catalog slug allocator into the operation-scoped mutation sessions
- Create Infrastructure exact-constraint/concurrency classifier
- Add PersistenceErrorMappingBehavior
- Create focused Unit/PostgreSQL tests

- [x] Stable Thai errors cover duplicate display, duplicate English, not found, archived, missing media, stale version and commit outcome unknown; Brand/Universe codes remain distinct.
- [x] A dedicated catalog-reference mutation lock serializes name checks; slug allocation retains one responsibility and excludes current ID on edit.
- [x] Persistence mapper runs outside the rolled-back transaction/coordinator, maps only exact named normalized-name constraints and concurrency failures, and uses the request Result factory. Unknown failures bubble.
- [x] Final pipeline order is Logging → Authorization → Validation → PersistenceErrorMapping → optional automatic Transaction → Handler; all Brand/Universe mutations still bypass the automatic transaction and use their fresh sessions.
- [x] Retire/adapt the old scoped CatalogSlugAllocator registration so allocation is always constructed against the active mutation session's fresh context; DI lifetime tests preserve scoped Identity/startup behavior while rejecting catalog use of the circuit context.
- [x] Concurrent normalized duplicates yield one success/one typed duplicate; distinct English names with the same slug base allocate deterministic suffixes.
- [x] Archived names/slugs remain reserved; English edit reallocates slug atomically with details/media/version.

## Task 6: TDD Brand list query

- [x] Create `ListBrands` query/validator/handler/read model returning `Result<PagedResult<BrandListItem>>`.
- [x] Project ID, names, slug, media URL/alt, lifecycle/readiness, version, real Product count and UTC update instant through the Infrastructure read port using a fresh no-tracking context.
- [x] FluentValidation owns search <=200, status enum, page >=1 and page size 1..100 through structured Result failures.
- [x] Tests cover default Active, Archived, All, normalized display/English/slug search, total/page predicate consistency, stable ordering, zero results and canonical beyond-last clamping.

## Task 7: TDD Universe list query

- [x] Mirror Brand query behavior through a distinct slice/read port and expose real Product/Character counts separately.
- [x] Tests prove fixed seed identities and truthful active/missing-logo readiness until edited.
- [x] Share only primitive filter/status values, not one combined business handler.

## Task 8: TDD Brand create/update/archive

- [x] Create requires valid names and exactly one upload; authorize before storage, lock/recheck both names, allocate slug, generate trusted Thai alt text and persist version 1.
- [x] Update requires ID/expected version, accepts optional replacement only when current media exists, updates alt text on rename, and reallocates slug when English changes.
- [x] Create/update use the explicit media coordinator/session; duplicate/stale/archived/not-found paths preserve selected UI state and compensate only when commit outcome proves safe.
- [x] Successful replacement deletes old media post-commit; delete failure records the trusted old key and still returns success.
- [x] Archive owns a fresh mutation session (without media staging), is terminal, preserves media/FKs and succeeds with zero or many Product references.
- [x] PostgreSQL races cover create/create, update/update and update/archive; losers return typed Results and leak neither tracking state nor unsafe media deletion.

## Task 9: TDD Universe create/update/archive

- [x] Mirror Brand behavior with Universe codes, `โลโก้จักรวาล {DisplayName}` alt text and Product/Character preservation.
- [x] Logo-less seed edit requires a selected logo. Seed edit/archive otherwise follows the same audited/concurrent rules as custom rows.
- [x] Archive preserves both Product and Character FKs plus committed logo.

## Task 10: Micro-TDD reusable image, filter, empty-state and modal components

**Files:**

- Create `StoreSingleImageField` and one adjacent blob-preview JS module
- Create shared catalog-reference editor/list/archive presentation components
- Create one shared `AdminRequestExecutor` for interactive MediatR calls
- Modify AdminFilterBar, AdminContentStateView, AdminModal/StoreDialog and FormValidationStore as required
- Create renderer/source/JS tests

- [x] Single-image field reuses StoreFieldShell, hides native file UI behind a styled keyboard-accessible 44px action, shows Thai help `JPEG, PNG หรือ WebP ไม่เกิน 5 MB`, uses `accept` only as a picker hint and stages zero bytes on selection. Authoritative over-limit/signature/MIME storage failures map to the image field and summary without clearing its preview.
- [x] Blob preview/current fallback/filename/size/replace/cancel work; object URLs revoke on every lifecycle edge; no base64/remote asset/image bytes enter source or reports.
- [x] AdminFilterBar gains one compatible EditForm/EditContext query mode using StoreTextField/StoreSelectField without nested forms. Submit constructs canonical URL, omits Active/page 1, and browser back/forward restores controls. Shared select keeps `appearance:none` and custom arrow/focus/error states.
- [x] AdminContentStateView accepts EventCallback/RenderFragment actions: unfiltered Empty opens create modal; filtered Empty clears filters; focus behavior remains correct.
- [x] Shared editor has Thai focusable validation summary, field-specific clear-on-edit, read-only non-link `ส่วน URL (slug)` copy and no second validation authority.
- [x] Shared list composes AdminDataTable/StatusBadge/Pagination with real counts and accessible row actions; it has no Domain/Infrastructure coupling.
- [x] Archive modal starts focus on Cancel and describes irreversible/reference impact. Busy editor/archive blocks close/Escape and double-submit while preserving shared dialog focus return after completion.
- [x] AdminRequestExecutor catches marked unexpected MediatR exceptions once, converts them to `System.Unexpected` Thai UI failure without re-logging or escaping the Interactive Server event callback, logs/marks an unmarked unexpected exception once, and always rethrows cancellation. Tests cover this interactive boundary plus the HTTP GlobalExceptionHandler's unmarked path.

## Task 11: Build the Thai Brand Admin page

- [x] Replace placeholder while retaining explicit `CanAccessAdmin` and adding granular `CanManageProducts`; preserve Thai h1/header/create action and Catalog context pills, and update M4-05 placeholder/source contracts intentionally. Direct SSR/enhanced shell redirect tests and granular use-case denial tests remain independent.
- [x] URL-driven shared filters and all Loading/unfiltered Empty/filtered Empty/Error/Ready states use Brand-specific Thai copy and canonical pagination.
- [x] Columns show image, names, non-link slug, readiness/status, real Product count, Bangkok update time and accessible edit/archive actions.
- [x] Create/edit/archive stay on list page, preserve input/blob preview after failure, map structured field/business/system errors and prevent double-submit.
- [x] Success toast reloads authoritative data and returns focus to connected opener or h1 when filtering/reordering removes it.

## Task 12: Build the Thai Universe Admin page and update source-of-truth docs

- [x] Mirror Brand UI with logo labels, Product/Character counts and seed `ต้องเพิ่มโลโก้` readiness; no Character/Category management affordance.
- [x] Cancellation/versioning prevents stale URL-query responses from replacing newer state after navigation/disposal.
- [x] Update `DOMAIN_RULES.md`, `ARCHITECTURE.md`, commerce specification and DESIGN_SPEC for English-edit slug regeneration, archive-while-referenced, seed rules, per-operation contexts, concurrency version, commit ambiguity and cleanup-ledger/recheck semantics.
- [x] Update local/backup docs for the new ledger/migration only where operationally relevant.

## Task 13: Reproducible real-Chrome/accessibility verification

- [x] Retain a standard-library real-Chrome automation script, exact command and secret-free JSON report; use only real production routes with deterministic temporary DB/media setup and teardown.
- [x] Cover 390/768 full-screen one-column editor, 900/1199/1200 list/modal/rail layout, 44px targets, local table overflow and no body overflow.
- [x] Keyboard covers filters/custom select/file, table scroller, row actions, cancel-first archive, busy Escape block, validation-summary focus, focus return/fallback and reduced motion.
- [x] Prove default Active/page 1 URL omission, submit/clear/back-forward restoration, seed missing-logo state and SSR Thai heading/content.
- [x] Prove blob preview with zero storage writes before submit, cancel/revoke, Thai validation/duplicate/stale/commit-unknown feedback and create/edit/archive success.
- [x] Report excludes credentials, tokens, cookies, local filenames/paths and image bytes; cleanup proves no app/Chrome/temp DB/media process remains.

## Task 14: Full verification and independent reviews

- [x] Run focused Domain, Result/authorization/validation/logging, operation-context, transaction/media/ledger, Brand, Universe, renderer/source and JS contract tests.
- [x] Run real PostgreSQL concurrency/reference/commit-outcome/migration tests and inspect media/ledger state.
- [x] Run format, warnings-as-errors build, full Unit/Integration, vulnerability scan, Compose validation and EF pending-model gate.
- [x] Verify no EF/Npgsql/Infrastructure reference in Application, no shared catalog circuit context, generic repository, Razor DbContext, fake count, hard delete, UI package, default browser select/file or duplicate dialog.
- [x] Obtain independent specification/accessibility review and authorization/concurrency/media review; fix all findings with RED/GREEN evidence.
- [x] Mark M4-06 complete only after fresh root verification, set Current Focus M4-07 and continue without pausing.

## Explicit non-goals

- No Product CRUD/image reorder, Character autocomplete/create, Inventory, Storefront query, cart or checkout.
- No Category page, hard delete, unarchive, user-editable slug, slug history/redirect, bulk action or import/export.
- No background cleanup worker, scheduler, Redis, object storage or separate database.
- No public Brand/Universe storefront route in this milestone.

## Completion evidence

Pending independent plan re-review, TDD implementation, reproducible browser verification and independent final reviews.
