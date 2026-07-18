# M4-04 Staged Local Media Storage Plan

**Goal:** Deliver a provider-neutral staged image-storage boundary and a secure single-server `LocalFileStorage` implementation that validates uploads while streaming, commits immutable media atomically, cleans staging failures/orphans, and serves only committed media through a safe public endpoint.

**Architecture:** Application owns immutable storage contracts and stable expected-validation errors. Infrastructure owns `System.IO`, signature detection, path/key validation, local persistence, options, startup initialization and stale-staging cleanup. Web maps the anonymous media endpoint through `IFileStorage`; Razor and Application never receive a physical path or ASP.NET upload type. There is no database migration, UI, Product handler, background worker, object storage or backup service in this milestone.

**Locked decisions:**

- “5 MB” means inclusive 5 MiB (`5 * 1024 * 1024` bytes). Never trust `Stream.Length`, request length, client extension or client filename.
- Accept only canonical `image/jpeg`, `image/png` and `image/webp` values, case-insensitively and without MIME parameters/aliases. The declared MIME must match the detected signature.
- JPEG requires `FF D8 FF`; PNG requires the exact eight-byte signature; WebP requires `RIFF`, `WEBP`, and a `VP8 `, `VP8L` or `VP8X` chunk marker. Signature validation, not full image decoding/re-encoding, is the approved v1 boundary.
- Stage a batch into `.staging/{batch-id}` and return descriptors in input order. Each descriptor exposes only an opaque batch token, generated immutable storage key/public relative URL, canonical MIME and byte length.
- Commit is one non-overwriting same-filesystem directory rename into `files/{batch-id}`. Generated keys use a fixed two-segment absolute-end grammar such as `{batch-id}/{file-id}.webp`; at least 128 random bits are used for both batch/file identities. Staging is never public.
- Storage enforces the global eight-file defensive batch cap while Product remains authoritative for max-eight, contiguous `SortOrder` and first-primary behavior. Expose/use one Product Domain constant instead of drifting literals.
- Expected empty/count/type/signature/MIME/size validation returns stable typed Thai `Result` failures. Permissions, disk, corrupt filesystem and unexpected stream I/O remain exceptions. Failure/cancellation cleanup uses an independent non-cancelled token and preserves cleanup errors with the original exception.
- `DiscardStagingAsync`, committed deletion and commit retry are idempotent. Invalid/forged keys never operate outside the configured tree.
- `Storage:RootPath` is an absolute persistent service-owned directory. Fixed `.staging` and `files` children share its filesystem. Production rejects a root under the deployment/web root and rejects root/child/final symlinks or reparse points. Initialization creates/probes the tree and fails startup when configuration/permissions/cleanup are unsafe.
- Staging retention defaults to 24 hours. Startup performs one stale-staging cleanup before serving requests; immediate operation failures also clean themselves. No scheduler/worker and no automatic deletion of unreferenced committed files.
- `/media/{batch-id}/{file-name}` is anonymous and serves only committed immutable keys. Draft media is public to a holder of its unguessable URL in v1; Draft queries must still never expose the URL. Responses use detected MIME, `nosniff`, immutable one-year caching, stable ETag/Last-Modified and framework range/conditional handling. Unsupported methods are rejected.
- Backup remains operational: briefly stop/quiesce ToyStore writes, then archive PostgreSQL, committed uploads and Data Protection keys as one restore set under `/var/backups/toystore`; restart only after both artifacts succeed and keep an off-server copy. Staging need not be backed up. No `Storage:BackupPath` or application backup provider is introduced.
- Filesystem/PostgreSQL atomicity is not claimed. Before M5-03 completes, the transaction pipeline must gain explicit media compensation around database save/commit; process-crash orphans require age-graced reconciliation, never destructive startup guessing. If deletion of replaced media fails after the database commit, log/record the orphan and return the already-committed success rather than presenting a false rollback or encouraging a duplicate retry.

## Task 1: RED provider-neutral contracts and architecture limits

**Files:**

- Create: `src/ToyStore.Application/Common/Files/IFileStorage.cs`
- Create: `src/ToyStore.Application/Common/Files/IFileStorageInitializer.cs`
- Create: `src/ToyStore.Application/Common/Files/MediaUpload.cs`
- Create: `src/ToyStore.Application/Common/Files/StagedMediaBatch.cs`
- Create: `src/ToyStore.Application/Common/Files/StagedMedia.cs`
- Create: `src/ToyStore.Application/Common/Files/StoredMediaRead.cs`
- Create: `src/ToyStore.Application/Common/Files/MediaStorageErrors.cs`
- Modify: `src/ToyStore.Domain/Products/Product.cs`
- Create/modify tests under `tests/ToyStore.UnitTests/Architecture`, `Application` and `Domain/Products`

- [x] Define batch-first stage/commit plus idempotent discard/delete, read and stale-cleanup operations with `CancellationToken` on every async call.
- [x] Keep contracts free of `IFormFile`, `IBrowserFile`, ASP.NET/Infrastructure types, physical paths and caller-selected destination filenames.
- [x] Use defensive immutable/copying models so caller mutation cannot change a staged batch after validation.
- [x] Add stable Thai validation errors for empty batch, over eight, unsupported MIME/signature, MIME mismatch and over-size.
- [x] Replace Product’s literal image limit with one public Domain constant and keep its existing invariant tests green.
- [x] Architecture tests prove Application stays EF/Npgsql and physical-filesystem API free (`Stream` is allowed; `File`, `Directory`, `Path`, physical providers and implementation paths are forbidden) and no generic file repository/service bucket appears.

## Task 2: Micro-TDD streaming image detection and bounded copy

**Files:**

- Create: `src/ToyStore.Infrastructure/Storage/ImageSignatureValidator.cs`
- Create: `src/ToyStore.Infrastructure/Storage/BoundedImageWriter.cs`
- Create: `tests/ToyStore.UnitTests/Infrastructure/Storage/ImageSignatureValidatorTests.cs`
- Create: `tests/ToyStore.UnitTests/Infrastructure/Storage/BoundedImageWriterTests.cs`

- [x] RED/GREEN JPEG, PNG and WebP canonical signatures and derived extensions/MIME.
- [x] Reject empty/truncated JPEG/PNG/WebP, GIF/SVG/arbitrary bytes, unknown MIME, MIME parameters/aliases and signature/MIME mismatch.
- [x] Stream with a bounded pooled buffer, read at most max+1, support non-seekable/short-read streams, allow exactly 5 MiB and reject 5 MiB+1.
- [x] Do not dispose caller-owned input streams; always close/delete locally created partial output on validation failure, exception or cancellation.
- [x] Avoid ImageSharp/full decode, thumbnails, EXIF work, antivirus, hashes and new image packages.

## Task 3: TDD generated keys, root containment and batch staging

**Files:**

- Create: `src/ToyStore.Infrastructure/Storage/LocalFileStorageOptions.cs`
- Create: `src/ToyStore.Infrastructure/Storage/StorageKey.cs`
- Create: `src/ToyStore.Infrastructure/Storage/LocalFileStorage.cs`
- Create: `src/ToyStore.Infrastructure/Storage/MediaIdGenerator.cs` or a minimal internal deterministic test seam
- Create: `tests/ToyStore.UnitTests/Infrastructure/Storage/LocalFileStorageStageTests.cs`
- Create: `tests/ToyStore.UnitTests/Infrastructure/Storage/StorageKeyTests.cs`

- [x] Validate absolute configured root, strict generated key/token grammar and canonical full-path containment with directory-separator boundaries.
- [x] Reject absolute/relative traversal, `.`, `..`, slash/backslash variants, NUL, Unicode separator tricks, unknown extensions and any reparse/symlink in fixed or key-derived paths.
- [x] Create each batch exclusively, generate collision-resistant immutable names/extensions and never use an original client filename.
- [x] Prove generated storage keys/public URLs remain comfortably within the current 500/1000-character database limits.
- [x] Preserve upload order exactly for batches one through eight and prove concurrent staging produces distinct keys.
- [x] If item N fails or cancellation/stream I/O occurs, remove the complete batch including items 1..N and leave no `.writing`/ready residue. Preserve original plus cleanup failures when both occur.

## Task 4: TDD atomic commit, idempotent lifecycle, reads and orphan cleanup

**Files:**

- Modify: `src/ToyStore.Infrastructure/Storage/LocalFileStorage.cs`
- Create: `tests/ToyStore.UnitTests/Infrastructure/Storage/LocalFileStorageLifecycleTests.cs`
- Create: `tests/ToyStore.UnitTests/Infrastructure/Storage/LocalFileStorageSecurityTests.cs`

- [x] Commit a ready batch with one non-overwriting same-filesystem directory move; a retry recognizes the same committed batch structurally by strict batch/key set, byte lengths, extensions and canonical signatures. Byte-for-byte tamper detection is not claimed without hashes; the service-owned filesystem remains the boundary. Staging/final collision or structural mismatch fails closed.
- [x] Prove commit failure leaves no partial final batch and preserves/discards staging according to the documented retry contract.
- [x] Make discard and committed-key/batch deletion idempotent; delete no outside sentinel on forged keys, symlink targets or partial cleanup failures.
- [x] Open only strict committed regular files, reject staging/missing/directory/symlink paths, and return canonical MIME, length, last-modified and stable ETag with a caller-disposable stream.
- [x] Cleanup deletes only complete/partial staging batches older than an explicit UTC cutoff, retains recent staging and all committed files, and is idempotent. Cancellation cannot skip cleanup already required by a failed operation.
- [x] On Unix, reject an existing root or fixed `.staging`/`files` child whose mode exceeds `0750` (group-write or any other permission), without chmod or content mutation; create missing root/fixed children with `0750` in the create syscall and do not mutate ancestor directories.

## Task 5: Options, DI, startup initialization and isolated test roots

**Files:**

- Modify: `src/ToyStore.Infrastructure/DependencyInjection.cs`
- Create: `src/ToyStore.Infrastructure/Storage/LocalFileStorageOptionsValidator.cs`
- Create: `src/ToyStore.Web/Startup/FileStorageStartupExtensions.cs`
- Modify: `src/ToyStore.Web/Program.cs`
- Modify: `tests/ToyStore.IntegrationTests/Infrastructure/ToyStoreWebApplicationFactory.cs`
- Create/modify registration, startup and configuration tests

- [x] Bind/validate `Storage:RootPath` and configurable 24-hour retention with startup validation; register one thread-safe singleton behind both Application boundaries.
- [x] Initialize/probe root and children before accepting requests, then run stale staging cleanup once. Missing/bad/symlink/unwritable production storage prevents startup with a non-secret diagnostic.
- [x] Keep local `.data/uploads` supported, production `/var/lib/toystore/uploads` outside the release tree, and isolate/delete a unique temporary storage root per Web integration factory.
- [x] Add a ready health contribution only if it reports the already-initialized local root without writing on every probe; otherwise startup fail-fast is sufficient.
- [x] Prove bootstrap-admin and ordinary startup share safe initialization and no background hosted cleanup loop is introduced.

## Task 6: Secure public media endpoint

**Files:**

- Create: `src/ToyStore.Web/Media/MediaEndpointExtensions.cs`
- Modify: `src/ToyStore.Web/Program.cs`
- Create: `tests/ToyStore.IntegrationTests/MediaEndpointTests.cs`
- Create/modify Web source/architecture tests

- [x] Map anonymous GET/HEAD only at `/media/{batchId}/{fileName}` through `IFileStorage.OpenReadAsync`; retain `MapStaticAssets()` for build assets and never expose a physical provider/root or directory listing.
- [x] Return 404 for invalid/missing/staging/traversal/double-encoded/unknown-extension/symlink keys and 405 for POST/PUT/DELETE.
- [x] Serve exact bytes with canonical Content-Type/Length, `X-Content-Type-Options: nosniff`, immutable cache, stable ETag/Last-Modified and no body for HEAD.
- [x] Use framework range/conditional support; prove 206 valid range, 416 unsatisfiable range and 304 matching ETag.
- [x] Ensure file streams are disposed after completion/aborted responses and endpoint failures do not leak physical paths.

## Task 7: Documentation and future transaction seam

**Files:**

- Modify: `docs/ARCHITECTURE.md`
- Modify: `docs/LOCAL_DEVELOPMENT.md`
- Modify: `docs/DEPLOYMENT.md`
- Modify: `deploy/toystore.service.example`
- Modify: `TASKS.md`
- Create/modify documentation contract tests

- [x] Document exact directory layout, canonical formats/limits, startup cleanup, public immutable URL behavior and that staging is excluded from public serving/backups.
- [x] Add `UMask=0027`, verify `ReadWritePaths`, production `Storage__RootPath`, backup/restore ownership and off-server copy guidance; do not add an application backup worker/provider.
- [x] Add an explicit M5-03 acceptance item: media coordination must commit new files before DB save/commit, compensate new media if save/commit fails with non-cancelled cleanup, and delete replaced old files only after durable commit. A post-commit deletion failure is logged/recorded for age-graced reconciliation while the command returns its committed success; tests must prove clients do not receive a false rollback/retry signal. Never claim cross-resource atomicity.
- [x] Change the manual backup runbook to quiesce/stop the service while both PostgreSQL dump and committed-media/key archive are captured, restart only after success, and name both artifacts as one restore set. Documentation contracts enforce this now; M10-05/M11-08 retain the clean-environment restore drill and reboot-recovery acceptance rather than claiming a production restore drill in M4.
- [x] Record that automatic startup reconciliation never deletes unreferenced committed files; future explicit reconciliation needs PostgreSQL comparison and an age grace period.

## Task 8: Full verification and independent reviews

- [x] Run focused signature/stream/storage/security/endpoint/configuration tests, then Product regressions.
- [x] Run format, warnings-as-errors CI build, full Unit and Integration suites, vulnerability scan and Compose validation.
- [x] Verify no EF model/migration change, no runtime network/image package, no worker/scheduler and no media path under `wwwroot`/release output.
- [x] Perform manual temp-root inventory after valid, invalid, cancellation, commit, delete and cleanup flows; assert no unexpected staging/committed residue.
- [x] Obtain independent spec review and fix every gap with RED/GREEN tests.
- [x] Obtain independent security/code-quality review and fix every Critical/Important finding.
- [x] Mark M4-04 complete only after fresh root verification; set Current Focus M4-05 and Next Task M4-06.

## Explicit non-goals

- No Product/Brand/Universe create/update handler, FluentValidation form, upload UI or database metadata transaction.
- No image transformation, decompression/dimension validation, thumbnail, EXIF stripping, antivirus, deduplication, resumable/chunk upload or client-original filename persistence.
- No object storage/CDN, network service, distributed lock, worker/scheduler, native Linux `openat2` wrapper or application backup provider.
- No automatic deletion of committed files based only on filesystem age or a startup guess.

## Completion evidence

Completed 2026-07-18 after TDD and two independent review cycles.

- RED/GREEN review fixes covered crash-recoverable staging-to-files probing, retained batch ownership, deterministic collision races, complete signature reads, disappearance races, stream disposal, CSPRNG 128-bit identifiers, canonical ancestor handling and Unix permission-boundary validation.
- Existing storage root and fixed children are validation-only on Unix and reject permissions exceeding `0750`; missing root/fixed children are created with `0750` in the create syscall without changing ancestor permissions.
- Fresh root verification: focused media Unit 110/110; focused endpoint/startup Integration 13/13; full Unit 462/462; full Integration 102/102; format clean; warnings-as-errors CI build 0 warnings/errors.
- NuGet vulnerability scan clean, Compose configuration valid, EF reports no pending model changes, and scope scans found no worker/scheduler, object storage, `EnsureCreated` or media exposure under `wwwroot`.
- Storage-specific temporary roots contained no unexpected staging or committed residue.
- Final independent specification and security/code-quality re-reviews both APPROVED with no Critical, Important or Minor findings.
