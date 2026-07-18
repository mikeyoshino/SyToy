# M4-07 Character Search and Inline Creation Plan

**Goal:** Deliver the authorized Character search/create vertical slices and a reusable Thai-first searchable multi-select autocomplete that Product create/edit can compose without adding a Character management page.

**Architecture:** Character remains Universe-scoped and creation-only. Application owns one query and one command plus narrow provider-neutral reader/mutation-session ports. Infrastructure uses `IDbContextFactory<ApplicationDbContext>` so each autocomplete search and mutation owns a fresh context; the create session owns one transaction, locks the target Universe row, applies exact constraint mapping and performs commit-outcome verification. Web owns only reusable input/presentation state and callback orchestration; Product wiring remains M5-04.

## Locked decisions

- Character has one required `UniverseId` and one required display `Name`; it has no slug, image, lifecycle page, edit, archive or hard-delete action in v1.
- Name is trimmed for persistence, Form-KC normalized through `CatalogNameNormalizer`, limited to 200 characters in both persisted and normalized form, and unique by `(UniverseId, NormalizedName)`.
- Active Universe is required for both search and inline create. Missing or archived Universe returns one typed safe Thai `Character.UniverseUnavailable` failure. Archived references never enter a new Product selection.
- Search is scoped to exactly one Universe, uses normalized contains matching, orders exact match first then normalized name then ID, returns at most 20 items, and allows null/empty/whitespace terms to populate the first options without calling the required-name normalizer. It never exposes Characters from another Universe.
- Query and command independently enforce `CanManageProducts` before validation/database work. Razor/menu visibility is not a security boundary.
- Create locks the target Universe row `FOR UPDATE`, checks Active while holding that lock through commit, then performs a normalized duplicate pre-check. This conflicts with Universe update/archive row locking and establishes the linearization order without a second advisory-lock hierarchy. The existing unique index `UX_Characters_UniverseId_NormalizedName` remains final race protection. Both duplicate paths map to `Character.DuplicateName`; no expected duplicate exception escapes to UI/logs.
- Character mutations avoid the circuit-scoped `IApplicationDbContext`/automatic transaction behavior. A once-only operation session owns a fresh context/transaction and releases it before fresh commit verification.
- Commit acknowledgement loss is reconciled by fresh `(Id, UniverseId, NormalizedName, Name)` evidence. An exact row confirms committed success; absent, unavailable or inconsistent evidence returns safe `Persistence.CommitOutcomeUnknown` and instructs refresh before retry. Creation-only Character verification never reports `Superseded`.
- The shared autocomplete is generic over its option value/model and is reusable by Product create/edit. It exposes callback ports whose search response carries server-authoritative `OfferInlineCreate` metadata, plus inline create and a typed copy/label contract; it never injects `ISender`, EF, Character normalization or Character business nouns.
- The control uses existing Store field/form primitives and Muted Ocean tokens. It has a styled text input, selected chips, listbox and inline-create action; no default browser select, UI package or second validation authority.
- Keyboard contract: input is a combobox; ArrowDown/ArrowUp moves the active option, Enter selects the active option or creatable pseudo-option, Escape closes, Backspace on an empty input removes the last chip, Tab leaves naturally, and selected options cannot be duplicated. Shortcut handling pauses during IME composition. Inline create is a direct `role=option` child of the listbox with stable ID, `aria-selected=false` and busy `aria-disabled`; it contains no nested button/control, participates in arrow/active-descendant navigation, and Enter or pointer selection creates then selects.
- Async search uses a locked default 250 ms debounce behind an injectable deterministic delay seam, then is cancellable and generation-checked so stale responses never replace a newer Universe/term. Incoming `Values` remain authoritative. Universe changes cancel/invalidate before awaiting one immutable empty `ValuesChanged` snapshot, close/reset state, and reconcile only from new parameters.
- Thai loading/empty/progress/selection/removal/create feedback uses a polite `role=status`; actionable errors use `role=alert`. Combobox/listbox relationships, `aria-multiselectable=true`, per-option `aria-selected`, existing-visible-only active descendant, Thai chip-removal names, help/error described-by wiring, 44px targets, visible focus and reduced motion are mandatory.
- No Product route/modal, Character Admin route/nav item, migration, audit schema, Redis, background worker, UI framework or JavaScript dependency is introduced in M4-07.

## Task 1: RED Character contracts, limits, errors and authorization

**Files:**

- Modify `Character.cs` and Character Domain tests for the shared name preparation contract
- Create `Characters/CharacterErrors.cs`, `CharacterOption.cs`, `SearchCharactersResult.cs` and authorized request base
- Modify common persistence target/kind contracts for the exact Character duplicate
- Create provider-neutral reader and mutation-session contracts
- Modify architecture tests

- [x] RED proves empty IDs/name, trimmed and normalized 200/201 boundaries, same normalized name in different Universes, and creation-only immutability.
- [x] GREEN makes `Character.Create` use `CatalogReferenceLimits.PrepareName` so both persisted and Form-KC normalized 200-character limits are Domain invariants and the prepared trimmed/normalized values are stored atomically.
- [x] Define `CharacterOption(Id, UniverseId, Name)`, `SearchCharactersResult(Items, HasExactMatch)` and typed `DuplicateName`/`UniverseUnavailable` failures with Thai safe copy. Exact match is authoritative Application metadata, not a Web normalization decision.
- [x] Query/command use `CanManageProducts`; unauthorized/forbidden requests short-circuit before validation and every persistence port.
- [x] Ports expose no EF/Npgsql/`IQueryable`/`DbSet`; architecture rejects a generic repository, Razor DbContext and circuit-scoped Character context.

## Task 2: RED normalized Character search slice and fresh reader

**Files:**

- Create `Characters/SearchCharacters/` query, handler and FluentValidation validator
- Implement `CharacterSearchReader` with fresh no-tracking contexts
- Register Infrastructure implementation
- Add Unit and PostgreSQL integration tests

- [x] Validator requires non-empty Universe ID, bounds only nonblank normalized terms and validates limit with Thai field failures; authorization runs first.
- [x] Handler maps null/empty/whitespace to normalized empty without calling `CatalogNameNormalizer`, normalizes each nonblank term once, returns server-authoritative `HasExactMatch`, and maps unavailable Universe to the typed failure without leaking cross-Universe rows.
- [x] Reader checks Active Universe and returns at most 20 scoped rows ordered exact normalized match first, then normalized name and ID; empty term is supported.
- [x] Null, empty and whitespace searches return the same initial options; nonblank searches are case/width/combining-mark/whitespace equivalent, cancellation propagates, and overlapping searches use distinct contexts with no tracking leakage.
- [x] PostgreSQL tests cover zero/many/capped results, cross-Universe isolation, archived/missing Universe and deterministic ordering.

## Task 3: RED inline CreateCharacter slice and transaction-safe persistence

**Files:**

- Create `Characters/CreateCharacter/` command, handler and FluentValidation validator
- Implement `CharacterMutationSession`/factory with conflicting Universe row lock
- Extend exact PostgreSQL persistence failure classification
- Add Unit, fault-injection and PostgreSQL race tests

- [x] FluentValidation is authoritative for Universe ID plus trimmed/normalized name boundaries and returns Thai field failures.
- [x] Handler opens one fresh once-only session, locks the target Universe row `FOR UPDATE`, rejects missing/archived Universe and normalized duplicate, creates the Domain entity, and returns a `CharacterOption`. The Universe row lock is acquired before Character reads/inserts and held through commit.
- [x] Add Character/DuplicateName to the provider-neutral persistence failure target/kind. The Create request implements `IPersistenceFailureResultRequest`, maps only that failure to `Character.DuplicateName`, and the exact classifier recognizes only `UX_Characters_UniverseId_NormalizedName`; unknown constraints/errors still throw to system handling.
- [x] Concurrent equivalent creates in one Universe produce one row plus one typed duplicate; distinct names in the same Universe and identical normalized names in different Universes both succeed. A forced unique violation through the full MediatR pipeline proves the exact classifier path independently of the pre-check.
- [x] Barriered create/archive races cover both linearizations without deadlock: create-first may commit before archive; archive-first makes create return `Character.UniverseUnavailable` and persist no Character. No create may succeed from a stale Active read after archive wins.
- [x] Rollback/cancellation/cleanup paths release transaction/context before rethrow or verification. Commit acknowledgement loss uses fresh non-cancellable exact evidence and returns committed success or safe commit-unknown without retrying the callback or inventing a Superseded state.
- [x] Expected validation/duplicate/unavailable failures are not Error-logged; unexpected faults remain logged exactly once by the existing ownership boundary.

## Task 4: RED reusable searchable multi-select state machine

**Files:**

- Create reusable autocomplete option/result/state types under Web shared forms
- Add focused pure state-machine tests

- [x] State prevents duplicate selected values, preserves selection order, removes explicitly/with empty-input Backspace and publishes immutable snapshots.
- [x] Arrow navigation skips no options and wraps or clamps consistently; active descendant is stable and caller-instance-owned, Enter selects exactly once, Escape closes and Tab does not trap focus.
- [x] The generic search completion consumes explicit `OfferInlineCreate` metadata mapped by its adapter from the Application result; generic/Web state owns no Character normalization or equivalence. Blank/existing/selected names never offer duplicate create.
- [x] Universe/owner key changes cancel and invalidate prior generations before emitting exactly one immutable empty snapshot, reset term/options/error and ignore old-owner or old-generation completions. Incoming controlled values remain authoritative until the parent supplies the new parameters.
- [x] The 250 ms debounce uses an injectable delay seam, cancels on term/owner/disposal and is deterministic under tests.
- [x] Composition start/end prevents Arrow/Enter/Escape/Backspace shortcuts from disrupting Thai/Japanese IME input.
- [x] Loading, success, empty, validation/business/system failure and busy-create transitions cannot leave stale options or double-submit enabled.

## Task 5: RED reusable Thai autocomplete component

**Files:**

- Create `StoreAutocompleteMultiSelect.razor` plus scoped CSS using existing Store field tokens
- Add renderer/source/accessibility tests

- [x] Component accepts option identity/display callbacks, cancellable async search returning items plus `OfferInlineCreate`, optional inline-create callback, authoritative selected values and `ValuesChanged`; it does not inject MediatR or persistence.
- [x] A typed copy contract supplies label, placeholder, loading, empty, create-label formatter, remove-label formatter and safe status/error wording. The shared source contains no Character business noun.
- [x] Implement as an EditContext-aware field using a `ValuesExpression`/stable `FieldIdentifier`, `EditContext.NotifyFieldChanged`, StoreFieldShell, external FluentValidation message display, `aria-invalid` and shared help/error `aria-describedby` wiring.
- [x] Render label/help/required marker, combobox input, selected removable chips, loading/empty/error region, listbox/options and the inline-create pseudo-option from supplied copy. The pseudo-option is a direct non-button `role=option` child, never a nested interactive control.
- [x] `role=combobox`, `aria-autocomplete=list`, `aria-expanded`, `aria-controls`, existing-visible in-listbox-only `aria-activedescendant`, `aria-multiselectable=true`, per-option `aria-selected`, pseudo-option `aria-selected=false`/busy `aria-disabled`, Thai removal labels, polite status and alert errors remain valid with zero/results/errors.
- [x] Keyboard/mouse selection, removal, pseudo-option inline create, DOM focus retention and blur ordering work without JavaScript. Busy create/search disables the pseudo-option and blocks duplicate action but not natural Tab navigation; IME composition is preserved.
- [x] CSS uses Noto Sans Thai/Muted Ocean tokens, 44px touch targets, visible focus, borderless hierarchy, mobile wrapping, `prefers-reduced-motion`, and no native select/default browser dropdown styling.

## Task 6: Character UI adapter and Product-ready contract

**Files:**

- Create a thin Admin Character autocomplete adapter/model only if needed to translate shared callbacks to `ISender`
- Add adapter and integration renderer tests; do not create a route

- [x] Adapter sends `SearchCharactersQuery`/`CreateCharacterCommand`, maps `HasExactMatch` to generic `OfferInlineCreate`, maps structured Result failures to Thai live feedback and preserves term/selections after duplicate/system/commit-unknown failures. Width/combining/whitespace-equivalent existing names suppress create while Web contains no Domain normalizer/reference.
- [x] Universe ID is authoritative input. Missing Universe disables search/create with `เลือกจักรวาลก่อน`; changing Universe cancels old requests and clears incompatible selections.
- [x] Successful inline create adds/selects the authoritative returned option exactly once and makes it discoverable by the next real PostgreSQL search.
- [x] Source contracts prove no `/admin/characters` route, navigation item, Character management table/dialog or Character query from Razor DbContext.

## Task 7: Verification, documentation and independent reviews

- [x] Run focused Domain, authorization/validation/logging, search/create/session/classifier, state/component/adapter and JS-free renderer tests.
- [x] Add a fake-data autocomplete specimen to `/design-system` only; extend a retained package-free real-Chrome harness for keyboard/mouse/focus/ARIA role containment, active-descendant pseudo-option navigation, Enter/pointer create, busy disable, 390/768/1200 wrapping and 44px targets, IME-safe key handling and reduced motion without creating Product UI.
- [x] Run real PostgreSQL normalization, scope, archived-Universe, create/create and create/archive races, forced exact-constraint pipeline mapping, rollback and commit-outcome tests; verify one durable row and clean contexts. At least one post-commit acknowledgement interceptor must run through the real `CreateCharacterHandler` + operation session + `CatalogCommitOutcomeResolver`, prove the callback executes once, resources release before a fresh verification context, and return the authoritative durable Character without retry.
- [x] Update `DOMAIN_RULES.md` for the prepared trimmed/normalized 200-character Character name and Active-Universe search/create rule; update `ARCHITECTURE.md` for fresh Character readers/sessions, Universe row-lock linearization and commit reconciliation. Update DESIGN_SPEC only if implementation changes its canonical shared autocomplete contract.
- [x] Run format, warnings-as-errors build, full Unit/Integration, vulnerability scan, Compose validation and EF pending-model gate; confirm no migration/model delta.
- [x] Verify no EF/Npgsql/Infrastructure reference in Application, shared circuit context, generic repository, Character route/page, raw/default dropdown or UI package.
- [x] Obtain independent specification/accessibility review and authorization/concurrency/persistence review; fix all findings with RED/GREEN evidence.
- [x] Mark M4-07 complete only after fresh root verification, set Current Focus M5-01 and continue without pausing.
