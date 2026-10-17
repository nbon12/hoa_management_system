# Tasks: Resident Architecture Request

**Input**: Design documents in `specs/029-resident-arc-requests/` (plan.md, spec.md, research.md, data-model.md, contracts/resident-architectural-applications.md, quickstart.md)
**Prerequisites**: 027-board-arc-review is merged on `main` (#209). This feature **extends** its code: `ArcApplicationFactory`, `ArcLocks`, `ArcEmailRenderer`, `ArcQueries`, `ApplicationsListEndpoint`, `ArcTestBase`.

**Tests**: REQUIRED. The spec's *Executable & living spec* clause and the repo's CLAUDE.md rule require that every acceptance scenario and Independent Test maps to a faithful automated test, including the denial scenarios. Each user story's tests are written first and must FAIL before implementation.

**Format**: `- [ ] T### [P?] [US#?] Description with file path`. `[P]` = different files, no dependency on an unfinished task.

**Path roots**: backend `HOAManagementCompany/`, backend tests `HOAManagementCompany.Tests/`, frontend `neko-hoa/src/app/`, Cypress `neko-hoa/cypress/e2e/`, Playwright `neko-hoa/e2e/`.

**Conventions to copy** (read before starting):
- Resident scope: `User.RequirePropertyId()` in `Features/Common/ClaimsPrincipalExtensions.cs`; endpoint shape in `Features/Property/OwnerGetEndpoint.cs`.
- ARC endpoint shape, `{code,message}` errors, `no-store`: `Features/Board/Architectural/ArcHttp.cs`, `CastVoteEndpoint.cs`, `InfoRequestEndpoint.cs`.
- Row lock: `ArcLocks.InLockedTransactionAsync` (`Features/Board/Architectural/ArcLocks.cs`).
- Outbox enqueue + dispatch after commit: `RecordOutcomeEndpoint.cs` lines ~78–102 (`ArcEmailRenderer.ToOutbox`, `OutboxDispatcher.DispatchPendingAsync`).
- Sensitive events: `Features/Board/Architectural/ArcLog.cs`.
- Validated options: `Infrastructure/Configuration/RateLimitingOptions.cs` + `AddValidatedOptions` in `Program.cs` (~line 182).
- Integration tests: `HOAManagementCompany.Tests/Integration/Board/Architectural/ArcTestBase.cs` (Testcontainers PostgreSQL + MinIO, `TestClock`, `CreatePropertyAsync`, `AppSpec` seeding), `ArcMigrationTests.cs`, `ArcTelemetryHygieneTests.cs`.
- Frontend: `features/board/architectural/*` (standalone components, signals, `arc-format.ts`, `arc-fixtures.ts`, `*.stories.ts`), `core/services/architectural.service.ts`.

---

## Phase 1: Setup

- [X] T001 Create the folders `HOAManagementCompany/Features/Property/Architectural/`, `HOAManagementCompany.Tests/Integration/Property/Architectural/`, `HOAManagementCompany.Tests/Unit/Architectural/` (if absent) and `neko-hoa/src/app/features/property/architectural/`
- [X] T002 [P] Add `ArcUploadOptions` (`SectionName = "Architectural:Uploads"`; `MaxFileBytes` = 52_428_800, `MaxFilesPerApplication` = 20, `MaxTotalBytes` = 262_144_000) and a FluentValidation `ArcUploadOptionsValidator` (all > 0; `MaxFileBytes` ≤ `MaxTotalBytes`) in `HOAManagementCompany/Infrastructure/Configuration/ArcUploadOptions.cs`; register with `AddValidatedOptions` in `HOAManagementCompany/Program.cs`; add the defaults under `Architectural:Uploads` in `HOAManagementCompany/appsettings.json`
- [X] T003 [P] Add `ResidentWritesPermitsPerMinute` (default 30, validated > 0) to `HOAManagementCompany/Infrastructure/Configuration/RateLimitingOptions.cs` and a `resident-writes` policy beside `board-writes` in `HOAManagementCompany/Program.cs`, partitioned the same way (`ClientIdentityResolver.ResolvePaymentsPartition`)
- [X] T004 [P] Add `ArcUploadOptionsValidatorTests` (`[Theory]`: defaults valid; zero/negative values invalid; per-file > total invalid) in `HOAManagementCompany.Tests/Unit/Architectural/ArcUploadOptionsValidatorTests.cs`

---

## Phase 2: Foundational (blocks every user story)

- [X] T005 Add `Withdrawn` to `HOAManagementCompany/Domain/Enums/ArcOutcome.cs` (update the comment: resident-initiated close, not a board decision). Then audit every `ArcOutcome` switch and conditional, backend and frontend (`grep -rn "ArcOutcome\|DecisionOutcome" HOAManagementCompany neko-hoa/src`), so `Withdrawn` never falls into a denial or approval branch:
  - `ArcEmailRenderer.OwnerKind` throws for `Withdrawn`.
  - `ArcQueries` decision mapping yields `{ outcome: "Withdrawn", wording: null, source: null }`.
  - The `DecisionOutcome` union type and `arc-format.ts` labels in `neko-hoa` show "Withdrawn".
- [X] T006 [P] Create entity `ArchitecturalApplicationDraft` (fields per data-model.md, `RemovedCarriedAttachmentIds` as `List<Guid>`, navigation `Attachments`, `Property`, `PreviousRevision`) in `HOAManagementCompany/Domain/Entities/ArchitecturalApplicationDraft.cs`, with a `REPOWISE:START domain=entities` marker
- [X] T007 [P] Create entity `ArchitecturalDraftAttachment` in `HOAManagementCompany/Domain/Entities/ArchitecturalDraftAttachment.cs`
- [X] T008 [P] Add nullable properties to the 027 entities:
  - `PlannedStartDate`, `PlannedCompletionDate`, `ContractorName`, `ContractorContact`, `AcknowledgedAt`, `WithdrawnAt`, `WithdrawnByUserId` in `HOAManagementCompany/Domain/Entities/ArchitecturalApplication.cs`.
  - `ResponseMessage`, `RespondedByUserId` in `ArchitecturalInfoRequest.cs`.
  - `InfoRequestId`, `UploadedByUserId` in `ArchitecturalAttachment.cs`.
- [X] T009 Configure the new and extended entities in `HOAManagementCompany/Infrastructure/Persistence/ApplicationDbContext.cs`:
  - DbSets `ArchitecturalApplicationDrafts` and `ArchitecturalDraftAttachments`.
  - String enum conversion for `ProjectType`.
  - Max lengths per data-model.md; a `uuid[]` column for `RemovedCarriedAttachmentIds`.
  - FKs and delete behaviors per data-model.md; index `(PropertyId)` on drafts; a filtered unique index on `PreviousRevisionId WHERE "PreviousRevisionId" IS NOT NULL`.
  - Check constraint "completion ≥ start" on drafts.
  - FK `ArchitecturalAttachments.InfoRequestId` → `ArchitecturalInfoRequests`, set null.
- [X] T010 Generate the migration with `dotnet ef migrations add AddResidentArcSubmission --project HOAManagementCompany` into `HOAManagementCompany/Infrastructure/Persistence/Migrations/`. Review it: it must only create the two tables and add nullable columns, FKs and indexes, and must not alter any existing column's nullability or the `(CommunityId, ApplicationNumber, Revision)` index. `Down` must reverse it cleanly.
- [X] T011 [P] Add `ResidentArcMigrationTests` (the new tables and columns exist; a 027-shaped seeded application round-trips unchanged; the drafts check constraint rejects completion < start; the unique `PreviousRevisionId` index rejects a second revision draft) in `HOAManagementCompany.Tests/Integration/Property/Architectural/ResidentArcMigrationTests.cs`, modeled on `ArcMigrationTests.cs`
- [X] T012 Extend `ArcNewApplication` in `HOAManagementCompany/Features/Board/Architectural/ArcApplicationFactory.cs` with optional trailing parameters `DateOnly? PlannedStartDate = null, DateOnly? PlannedCompletionDate = null, string? ContractorName = null, string? ContractorContact = null, DateTimeOffset? AcknowledgedAt = null`, and copy them in `NewRow`. 027 callers and `Seed/ArchitecturalSeeder.cs` must compile unchanged.
- [X] T013 [P] Extend `HOAManagementCompany.Tests/Integration/Board/Architectural/ArcApplicationFactoryTests.cs`: `CreateFromSettingsAsync` and `CreateRevisionAsync` persist the new resident fields, and leave them null when omitted
- [X] T014 Add `Task DeleteAsync(string storageKey, CancellationToken ct = default)` to `HOAManagementCompany/Infrastructure/Storage/IDocumentStorage.cs`. Implement it in `S3DocumentStorage.cs` (`DeleteObjectAsync`; a missing object is not an error) and in any other `IDocumentStorage` implementation or test double (`grep -rn ": IDocumentStorage" HOAManagementCompany HOAManagementCompany.Tests --include=*.cs`).
- [X] T015 [P] Write `ArcAttachmentValidatorTests` FIRST in `HOAManagementCompany.Tests/Unit/Architectural/ArcAttachmentValidatorTests.cs`. It must cover:
  - `[Theory]` over valid bytes for each allowed type, each mapped to its canonical content type: `%PDF-` → `application/pdf`; `FF D8 FF` → `image/jpeg`; `89 50 4E 47 0D 0A 1A 0A` → `image/png`; an ISO-BMFF `ftyp` box with brand `heic`/`heix`/`mif1`/`heif` → `image/heic`.
  - `[Theory]` over rejected content: a Windows PE (`MZ`) named `plan.pdf`, plain text named `photo.jpg`, a GIF, an empty file, and an `ftyp` box with brand `mp42` (MP4 video). Each yields `UNSUPPORTED_FILE_TYPE`.
  - `[Theory]` over limits with small options:
    - size = `MaxFileBytes` is accepted; `MaxFileBytes + 1` → `FILE_TOO_LARGE`;
    - existing count = `Max - 1` is accepted; `= Max` → `ATTACHMENT_LIMIT_REACHED`;
    - existing total + new = `MaxTotalBytes` is accepted; `+1` → `ATTACHMENT_LIMIT_REACHED`.
- [X] T016 Implement `ArcAttachmentValidator` in `HOAManagementCompany/Features/Property/Architectural/ArcAttachmentValidator.cs`:
  - `Sniff(ReadOnlySpan<byte> header)` returns the canonical content type or null.
  - `Validate(long size, byte[] header, int existingCount, long existingTotalBytes)` returns `(contentType, errorCode)` using `IOptions<ArcUploadOptions>`.
  - Add a `REPOWISE:START domain=resident-arc-uploads` marker.
  - T015 must pass.
- [X] T017 [P] Create `ResidentArcErrorCodes` (`NOT_FOUND`, `ACKNOWLEDGEMENT_REQUIRED`, `UNSUPPORTED_FILE_TYPE`, `FILE_TOO_LARGE`, `ATTACHMENT_LIMIT_REACHED`, `INFO_ALREADY_ANSWERED`; reuse `ArcErrorCodes` for `FORBIDDEN`, `VALIDATION_ERROR`, `APPLICATION_DECIDED`, `APPLICATION_CLOSED`, `ATTACHMENT_UNAVAILABLE`, and the factory's `REVISION_NOT_ALLOWED`) and all request/response DTOs from the contract in `HOAManagementCompany/Features/Property/Architectural/ResidentArcModels.cs`. Resident DTOs MUST NOT declare any vote, voter, tally or vote-comment property.
- [X] T018 Create `ResidentArcScope` in `HOAManagementCompany/Features/Property/Architectural/ResidentArcScope.cs`:
  - `LoadDraftAsync(db, draftId, propertyId)` and `LoadApplicationAsync(db, id, propertyId)` return the entity, or null when it is missing **or** on another property.
  - `ForbiddenAsync(HttpContext)` writes the non-disclosing `403 FORBIDDEN` body from the contract.

  The same 403 is returned for "not yours" and "doesn't exist", so existence is never revealed.
- [X] T019 [P] Create `ResidentArcLog` in `HOAManagementCompany/Features/Property/Architectural/ResidentArcLog.cs`. Use the `[LoggerMessage]` pattern from `ArcLog.cs` with the `SensitiveEvent` scope. It covers:
  - `ArcResidentAttachmentAccess` and `ArcResidentAccessDenied`;
  - `ArcUploadRejected`, with the reason code only, never the file name or bytes;
  - `ArcSubmitted`, `ArcWithdrawn`, `ArcInfoReplied` and `ArcRevisionDraftCreated`.

  Every event carries IDs only.
- [X] T020 Create `ResidentArcTestBase : ArcTestBase` in `HOAManagementCompany.Tests/Integration/Property/Architectural/ResidentArcTestBase.cs`. It provides:
  - `CreateResidentAsync(communityId)`: a property + `Owner` row + `ApplicationUser` linked by `UserProperty`; returns the user, property and an `HttpClient` authenticated with that `propertyId` claim, built with `BoardTestBase.CreateUserWithPropertyAsync` (login-capable user linked by `UserProperty`) plus an `Owner` row, then authenticated with `BoardTestBase.LoginAsync(email)` so the bearer token carries that `propertyId` claim.
  - `AddCoOwnerAsync(propertyId)`.
  - `CreateBoardMemberClientAsync(communityId)`, reusing `ArcTestBase`.
  - `ExtraConfiguration` overrides for small upload limits.
  - Byte fixtures: `ValidPdf`, `ValidPng`, `ValidJpeg`, `ValidHeic`, `ExeRenamedPdf`, `TextRenamedJpg`.
  - `MultipartFile(bytes, name)`.
- [X] T021 [P] Add the frontend models (`ResidentArcDraft`, `ResidentArcListItem`, `ResidentArcDetail`, `ResidentArcStatus` = `'Draft'|'Submitted'|'MoreInfoRequested'|'Approved'|'Denied'|'Withdrawn'`, `ArcProjectType`) in `neko-hoa/src/app/core/models/resident-arc.models.ts`. Export them from `core/models/index.ts`.
- [X] T022 [P] Create `ResidentArchitecturalService` with one method per contract endpoint, using the same `HttpClient` patterns and base URL as `architectural.service.ts`. Upload uses `FormData`. Put it in `neko-hoa/src/app/core/services/resident-architectural.service.ts`, with `resident-architectural.service.spec.ts` (`HttpTestingController`: each method hits the exact URL and verb from the contract).
- [X] T023 [P] Create `resident-arc-format.ts`: status labels, project-type labels ("Exterior paint", "Shed / outbuilding", "Windows / doors"), and the error-code-to-message map. Add `resident-arc-format.spec.ts` covering every status and error code. Both go in `neko-hoa/src/app/features/property/architectural/`.
- [X] T024 Add the routes to `neko-hoa/src/app/app.routes.ts` beside `property/owner`: `property/architectural` (list), `property/architectural/new`, `property/architectural/drafts/:draftId`, `property/architectural/:id` and `property/architectural/:id/revise`. All are lazy standalone components, so they match the owner links in 027's emails. Add an "Architectural requests" nav entry in `neko-hoa/src/app/shell/shell.component.ts` with the existing `property/*` entries. Update the shell spec if it asserts nav entries.

**Checkpoint**: migration applies; `dotnet build` and `npm run build` are green; T004, T011, T013 and T015 pass.

---

## Phase 3: User Story 1 — File an architectural request (P1) 🎯 MVP

**Goal**: A resident creates, edits and deletes drafts and submits one. Submission gets `ARC-<n>`, received and due dates, and a confirmation email, and the request appears in the board's Open list.
**Independent test**: create → edit → submit with only resident endpoints, then check the board list and outbox. No board UI is needed.

### Tests (write first; they must fail)

- [X] T025 [P] [US1] Create `HOAManagementCompany.Tests/Integration/Property/Architectural/FileRequestTests.cs` with one test per acceptance scenario, named after it:
  1. `Submit_CompleteDraft_CreatesOpenApplicationWithNumberDatesAndSnapshots` (AS1). Read `NextApplicationNumber` = N before submitting. Assert:
     - 201 with `displayId == $"ARC-{N}"`;
     - the DB row has `Status == Open`, `ReceivedDate == today` in the community time zone (from `TestClock`), and `DueDate == ReceivedDate + ReviewPeriodDays`;
     - `DecisionRule`, `LapseRule` and `TimeZoneId` equal the settings;
     - `SubmittedByUserId` == the caller; the planned dates and contractor equal the draft's values; `AcknowledgedAt` is not null;
     - the draft row is gone.
  2. `SaveDraft_IsPersistedAsDraft_AndNotInBoardOpenList` (AS2): a draft row exists with the sent fields, and a board member's `GET …/architectural-applications?status=open` has no item for this property.
  3. `EditDraft_PersistsUpdatedValues_AndStaysDraft` (AS3): PUT new values, then GET returns them; no application row exists.
  4. `DeleteDraft_RemovesDraftAndItsObjects` (AS4): after uploading a file and deleting the draft, the draft and attachment rows are gone, `IDocumentStorage.ExistsAsync(key)` is false, and the list doesn't contain it.
  5. `Submit_WithoutAcknowledgement_Refused_NoNumberAllocated` (AS5): 422 `ACKNOWLEDGEMENT_REQUIRED`; no application row; `NextApplicationNumber` is unchanged; the draft still exists.
  6. `CompletionBeforeStart_IsRefused_AndCanNeverBeSubmitted` (AS6): 422 `VALIDATION_ERROR`; no application row.
  7. `Submit_EnqueuesConfirmationEmailToSubmitter` (AS7): exactly one `OutboxMessages` row with `Kind == "arc_owner_submitted"`, `RecipientUserId` == the caller, `OwnerId == null` and `DedupKey == $"arc:{id}:submitted"`. Its payload subject and body contain `ARC-{N}`; the body has no "vote" text.
  8. `Submit_AppearsInBoardOpenList` (AS8): a board member's `GET …?status=open` contains the item with the same `id` and `displayId`. For SC-007, the resident's own `GET /property/architectural-applications`, sent right after the submit response, contains it with `status == "Submitted"`; a `Stopwatch` from the submit request to the list response must read under 5 s.
  9. `ConcurrentSubmits_GetDistinctSequentialNumbers` (SC-002 / edge case): 5 drafts in one community submitted in parallel get 5 distinct numbers that form a contiguous range.
  10. `Submit_DraftWithAttachment_CarriesAttachmentToApplication` (Independent Test / SC-001): the application's attachment row points at the draft's storage key, and the object still exists.
- [X] T026 [P] [US1] Add `OwnerSubmitted_RendersDisplayIdDatesAndNoBoardData` in a new `HOAManagementCompany.Tests/Unit/Architectural/ArcEmailRendererSubmittedTests.cs` (no renderer test class exists yet). It asserts the subject "We received your architectural request ARC-1042", that the body contains the received date, due date, project title, the "work may not begin until approved" reminder and the request link, and that a revision shows "v2".

### Implementation

- [X] T027 [US1] Add `ArcEmailKinds.OwnerSubmitted = "arc_owner_submitted"` and `ArcEmailRenderer.OwnerSubmitted(app, propertyAddress, ownerFirstName, recipientEmail, communityName)` returning an `AlertMessage`. Both go in `HOAManagementCompany/Features/Board/Architectural/ArcEmailRenderer.cs`. Add the kind to the `OutboxMessage.Kind` doc comment in `Domain/Entities/OutboxMessage.cs`.
- [X] T028 [US1] Implement `ResidentArcDraftService` in `HOAManagementCompany/Features/Property/Architectural/ResidentArcDraftService.cs`:
  - `CreateAsync`, `GetAsync`, `UpdateAsync`, `DeleteAsync`. Delete removes the draft-attachment objects via `IDocumentStorage.DeleteAsync` and then the rows.
  - Field validation per data-model.md: the project type parses to `ArcProjectType`; the length limits; completion ≥ start when both are present.
  - `CommunityId` comes from the property.
  - Add a `REPOWISE:START domain=resident-arc` marker.
- [X] T029 [US1] Implement `ResidentArcSubmitService.SubmitAsync(draftId, propertyId, userId)` in `HOAManagementCompany/Features/Property/Architectural/ResidentArcSubmitService.cs`:
  - Validate the required fields and the acknowledgement.
  - Inside the db execution strategy and a transaction, build `ArcNewApplication`:
    - `OwnerName` = `Owner.FirstName + " " + Owner.LastName` for the property, falling back to the user's name;
    - `ReceivedDate` = today in `CommunityArcSettings.TimeZoneId`, from the registered `TimeProvider`;
    - `AcknowledgedAt` = now;
    - the attachments mapped to `ArcNewAttachment` on the same keys.
  - Call `factory.CreateFromSettingsAsync`, or `CreateRevisionAsync(PreviousRevisionId, input, RemovedCarriedAttachmentIds)` when `PreviousRevisionId` is set.
  - Stamp `UploadedByUserId` on the new attachment rows; remove the draft and draft-attachment rows (keep the objects).
  - Add the outbox row via `ArcEmailRenderer.ToOutbox(ArcEmailKinds.OwnerSubmitted, …, $"arc:{id}:submitted", ownerId: null, recipientUserId: userId)` and `SaveChanges`; commit.
  - After commit: `OutboxDispatcher.DispatchPendingAsync` and `ResidentArcLog.ArcSubmitted`.
  - Let the factory's `DomainException` (`REVISION_NOT_ALLOWED`) surface as 409.
- [X] T030 [US1] Create the endpoints in `HOAManagementCompany/Features/Property/Architectural/`: `CreateDraftEndpoint.cs`, `GetDraftEndpoint.cs`, `UpdateDraftEndpoint.cs`, `DeleteDraftEndpoint.cs` and `SubmitDraftEndpoint.cs`, with the routes from the contract.
  - Each one: `RequirePropertyId()` → `ResidentArcScope` → service.
  - Writes use `Options(x => x.RequireRateLimiting("resident-writes"))`.
  - Every response is `no-store`, and `DomainException` maps to `{code,message}` with its status.
  - Tag them `WithTags("Architectural (resident)")`.
- [X] T031 [US1] Register `ResidentArcDraftService`, `ResidentArcSubmitService`, `ArcAttachmentValidator` and the reply/withdraw/query services as scoped next to the ARC registrations (~line 412) in `HOAManagementCompany/Program.cs`
- [X] T032 [P] [US1] Create the `request-form` component in `neko-hoa/src/app/features/property/architectural/request-form.component.ts`: The layout MUST be responsive (FR-027): one column under 600 px, tables become stacked cards on phones, and no fixed width may overflow 375 px.
  - The fields from the contract; the project-type `<select>` uses the labels from `resident-arc-format.ts`.
  - A completion ≥ start validator.
  - A required acknowledgement checkbox with the label "I understand work may not begin until this request is approved".
  - **Save draft** and **Submit**; Submit is disabled until valid.
  - Server error codes are mapped to messages; every control has a label, and errors are linked with `aria-describedby` (WCAG 2.1 AA).
  - Modes: `new`, `edit draft` (`drafts/:draftId`) and `revise`.
  - After submit, navigate to `property/architectural/:id`.
- [X] T033 [P] [US1] Add `request-form.component.spec.ts` (Angular Testing Library) in the same folder:
  - Submit is disabled without the acknowledgement.
  - Completion < start shows the date error and blocks submit.
  - Save draft calls `createDraft` then `updateDraft`.
  - Submit calls `submitDraft` and navigates.
  - A 422 `ACKNOWLEDGEMENT_REQUIRED` from the server shows the mapped message.
- [X] T034 [US1] Add the Cypress journey `neko-hoa/cypress/e2e/resident-arc-submit.cy.ts`: sign in as a resident (reuse `cypress/e2e/helpers`), open Architectural requests → New, fill the form, **Save draft**, reload and see the draft, check the acknowledgement, **Submit**, then assert the detail shows `ARC-` and "Submitted" and the list contains it. Follow `board-architectural.cy.ts` for setup.

**Checkpoint**: T025–T026 and T033 pass; a resident can file a request end to end.

---

## Phase 4: User Story 5 — Attachments are validated and private (P1)

**Goal**: Only genuine PDF, JPG, PNG and HEIC files within the environment limits are stored. Files are private and reachable only through short-lived links.
**Independent test**: upload valid, spoofed and oversize files through the API and inspect MinIO and the DB.

### Tests (write first)

- [X] T035 [P] [US5] Create `HOAManagementCompany.Tests/Integration/Property/Architectural/AttachmentTests.cs`, with small limits via `ExtraConfiguration`:
  1. `Upload_ValidFile_IsStoredPrivatelyWithSniffedMetadata` (AS1), a `[Theory]` over PDF, PNG, JPEG and HEIC fixtures. Assert:
     - 201 with `contentType` == the canonical sniffed type;
     - a row with `StorageKey` matching `arc/{communityId}/drafts/{draftId}/` and `SizeBytes` == the byte count;
     - `ExistsAsync(key)` is true.
  2. `Upload_ContentNotAllowed_RefusedAndNotStored` (AS2), a `[Theory]` over `ExeRenamedPdf` (sent as `plan.pdf` / `application/pdf`) and `TextRenamedJpg`. Assert 422 `UNSUPPORTED_FILE_TYPE`, no attachment row, and no object under the draft's prefix.
  3. `Upload_OverPerFileLimit_Refused` (AS3): 422 `FILE_TOO_LARGE`, no row, no object.
  4. `Upload_AtCountLimit_Refused` and `Upload_OverTotalBytes_Refused` (AS4): fill to the limit, then the next upload gets 422 `ATTACHMENT_LIMIT_REACHED`; the count is unchanged.
  5. `AttachmentUrl_IsShortLived_AndNeverEmbedded` (FR-013 / SC-004): `GET …/url` returns a URL whose `expiresAt` ≤ now + 15 min, and neither the draft GET nor the list JSON contains `http`.
  6. `DeleteDraftAttachment_RemovesRowAndObject` (FR-014).
  7. `Upload_ToAnotherPropertysDraft_Forbidden`: 403 `FORBIDDEN` with the contract body; no object written.
  8. `Upload_Rejected_LogsSensitiveEventWithoutFileName`: the `LogSink` has `ArcUploadRejected` with a reason code, and no log contains the file name.
  9. `Upload_StorageUnavailable_FailsCleanlyAndCanRetry` (edge case), in a separate class `AttachmentStorageFailureTests.cs` in the same folder. Its `ConfigureTestServices` wraps `IDocumentStorage` in a decorator whose `UploadAsync` throws while a toggle is on. Expect 503 `STORAGE_UNAVAILABLE`, no `ArchitecturalDraftAttachment` row, and an unchanged attachment count. Turn the toggle off and the same upload succeeds.

### Implementation

- [X] T036 [US5] Create `UploadDraftAttachmentEndpoint.cs` (multipart; read the `IFormFile` stream, reject an over-limit `Length` before buffering, read the header bytes, validate with `ArcAttachmentValidator` against the draft's existing and carried-over count and total, then `UploadAsync` **before** writing the row, so a storage failure leaves no row; storage exceptions return 503 `STORAGE_UNAVAILABLE`, a code added to `ResidentArcErrorCodes`), `DeleteDraftAttachmentEndpoint.cs` and `DraftAttachmentUrlEndpoint.cs` in `HOAManagementCompany/Features/Property/Architectural/`. Set a per-endpoint request body size limit of `MaxFileBytes` plus 1 MB multipart overhead (`[RequestSizeLimit]`-equivalent via `IHttpMaxRequestBodySizeFeature` or FastEndpoints `MaxRequestBodySize`). Rate-limit the writes. Log rejections.
- [X] T037 [P] [US5] Add attachment handling to `request-form.component.ts`:
  - A file input with `accept=".pdf,.jpg,.jpeg,.png,.heic,application/pdf,image/jpeg,image/png,image/heic"`.
  - A client-side size and count pre-check, with a comment that it is UX only.
  - A list of uploaded files with Remove buttons and per-file error messages.
  - Opening a file calls the URL endpoint, then `window.open(url, '_blank', 'noopener')`.

  Extend `request-form.component.spec.ts`: upload calls the service with `FormData`; a 422 `UNSUPPORTED_FILE_TYPE` shows its message; Remove calls `deleteDraftAttachment`.
- [X] T038 [US5] Add the Playwright spec `neko-hoa/e2e/resident-arc-attachments.spec.ts`. In a real browser, uploading a real PDF succeeds and lists it; uploading a `.txt` renamed `.pdf` shows the unsupported-type error. Follow `board-architectural.spec.ts` for setup.

**Checkpoint**: T035 and T038 pass.

---

## Phase 5: User Story 2 — Track my architectural requests (P2)

**Goal**: A list and a detail page with status, timeline, attachments and decision. Votes, voters and comments are never shown.
**Independent test**: seed applications in every state for one property (using `ArcTestBase.AppSpec`), then call the resident list and detail endpoints.

### Tests (write first)

- [X] T039 [P] [US2] Create `HOAManagementCompany.Tests/Integration/Property/Architectural/TrackRequestsTests.cs`:
  1. `List_ShowsEachRequestWithProjectedStatus` (AS1), a `[Theory]` over (seeded state → expected status):
     - a draft → `Draft`;
     - Open → `Submitted`;
     - DecisionReached → `Submitted`;
     - Open + unanswered info request → `MoreInfoRequested`;
     - Closed/Approved → `Approved`;
     - Closed/Denied → `Denied`;
     - Closed/Withdrawn → `Withdrawn`.

     Also assert only the latest revision of an `ApplicationNumber` is listed, and that `limit` and `offset` work (default 25, clamped to 100).
  2. `Detail_ShowsSubmittedFieldsTimelineAndAttachments` (AS2): the exact field values and the attachment list (`fileName`, `sizeBytes`); `timeline` lists `Submitted`, `InfoRequested` and `InfoReplied` in time order.
  3. `AttachmentUrl_ExpiresWithin15Minutes_NoDurableUrlInDetail` (AS3): `expiresAt - now ≤ 15 min`, and the detail JSON contains no `http`.
  4. `Detail_Denied_ShowsWordingReasonAndFormalStatement` (AS4): for a seeded Closed/Denied application with `DecisionWording = RevisionsRequested`, `OwnerReason` "Lower the fence to 5ft" and the settings statement, assert `decision.outcome == "Denied"`, `decision.wording == "RevisionsRequested"`, `ownerReason` equal to the seeded reason, `formalDisapprovalStatement` equal to the settings statement, and `canRevise == true`. Add a sibling case: an Approved application with conditions shows `conditionsOfApproval`.
  5. `ListAndDetail_NeverExposeVotesVotersOrComments` (AS5 / SC-005): seed two board votes whose comments contain the sentinels "SECRET-COMMENT" and the voter "Board Voter". The raw list and detail JSON must not contain `SECRET-COMMENT`, `Board Voter`, the voter user IDs, or the keys `votes`, `tally` or `voterName`.
  6. `DecisionReached_NotYetRecorded_ShowsSubmittedWithNoDecision`: the decision isn't official until the manager records it.
  7. `SubmittedApplication_CannotBeEdited` (FR-008 / edge case): `PUT`, `PATCH` and `DELETE` on `/property/architectural-applications/{id}` return 404 or 405, and every field of the DB row (title, description, dates, contractor, attachments) is unchanged. The only routes that change an application are reply, withdraw and revise.
- [X] T040 [P] [US2] Create `HOAManagementCompany.Tests/Integration/Property/Architectural/ResidentArcAuthorizationTests.cs` (FR-022–FR-024 / SC-006 / edge cases):
  1. `NonOwner_EveryEndpoint_Forbidden`, a `[Theory]` over every resident route and verb: for a draft or application on property A, a resident whose active property is B gets 403 `FORBIDDEN` with the exact contract body. For writes, assert the target is unchanged.
  2. `Forbidden_BodyIdentical_ForNonexistentId`: a random GUID returns the same 403 body, so existence isn't revealed.
  3. `CoOwner_CanViewWithdrawReplyAndRevise`: a second user linked to the same property can GET a request another owner created and can withdraw it.
  4. `BoardMemberOfCommunity_WhoIsNotOwner_Forbidden`: a board member of the same community calling the resident endpoints on someone else's request gets 403 (no widening, FR-023).
  5. `SwitchingActiveProperty_RescopesList`: a user owning two properties sees only the active property's requests.
  6. `CrossCommunity_Forbidden`: the same 403 for a request in another community.
  7. `Denial_LogsSensitiveEvent`: the `LogSink` contains `ArcResidentAccessDenied`.

### Implementation

- [X] T041 [US2] Implement `ResidentArcQueries` in `HOAManagementCompany/Features/Property/Architectural/ResidentArcQueries.cs`. Add a `REPOWISE:START domain=resident-arc` marker. It provides:
  - `ListAsync(propertyId, limit, offset)`: drafts, then the latest revision per `ApplicationNumber` ordered by `ReceivedDate` desc, using the shared `Paging` helper (default 25, max 100).
  - `ProjectStatus(app, hasUnansweredInfo)`, implementing the data-model table.
  - `BuildDetailAsync(app)`: fields, attachments, info requests (no requester identity), timeline, the decision block, `formalDisapprovalStatement` from `CommunityArcSettings` (only when Denied), `canWithdraw` and `canRevise`, and the revisions.

  It never projects `ArchitecturalVote`.
- [X] T042 [US2] Create `MyApplicationsListEndpoint.cs`, `MyApplicationDetailEndpoint.cs` and `ApplicationAttachmentUrlEndpoint.cs` in `HOAManagementCompany/Features/Property/Architectural/`. Scope through `ResidentArcScope`; the attachment URL returns 404 `ATTACHMENT_UNAVAILABLE` via `ExistsAsync`, logs `ArcResidentAttachmentAccess`, and is `no-store`.
- [X] T043 [P] [US2] Create `my-requests-page.component.ts`: a table of requests with a status chip (text label, not color alone), a "New request" button, drafts linking to `drafts/:draftId` and applications to `:id`, pagination, and an empty state. Add `my-requests-page.component.spec.ts` (ATL: renders each status label; the empty state; links) and `my-requests-page.stories.ts`, all in `neko-hoa/src/app/features/property/architectural/`. The layout MUST be responsive (FR-027): one column under 600 px, tables become stacked cards on phones, and no fixed width may overflow 375 px.
- [X] T044 [P] [US2] Create `request-detail.component.ts`: The layout MUST be responsive (FR-027): one column under 600 px, tables become stacked cards on phones, and no fixed width may overflow 375 px.
  - It shows the header (`displayId`, the `v{n}` badge, status), the submitted fields, the timeline list and the attachments (opened on demand through the URL endpoint).
  - The decision block shows the wording, reason and formal statement when Denied, or the conditions when Approved.
  - There is no vote, tally or comment UI.
  - The Withdraw and Revise buttons follow `canWithdraw` and `canRevise`.

  Add `request-detail.component.spec.ts` (ATL: denied shows the reason and statement; approved shows conditions; buttons follow the flags; nothing vote-related renders even when a fixture sneaks in a `votes` field) and `request-detail.stories.ts`, both in the same folder.
- [X] T045 [US2] Add a Cypress assertion to `neko-hoa/cypress/e2e/resident-arc-submit.cy.ts`: after submitting, the list shows the request as "Submitted / Under review" and the detail opens.

**Checkpoint**: T039–T040 pass; tracking works with seeded data in every state.

---

## Phase 6: User Story 3 — Respond to a request for more information (P3)

**Goal**: The resident sees the board's question on the request and on the dashboard, replies (optionally with files), the marker clears, and the clock keeps running.
**Independent test**: a board member posts an info request through 027's endpoint; the resident replies through 029's.

### Tests (write first)

- [X] T046 [P] [US3] Create `HOAManagementCompany.Tests/Integration/Property/Architectural/InfoReplyTests.cs`:
  1. `InfoRequested_ShownOnRequestAndDashboard` (AS1): a board member `POST`s 027's `…/info-requests` with "Please attach a plat survey". The resident detail's `infoRequests[0].message` equals it and the status is `MoreInfoRequested`. The resident `GET /dashboard` has `architecturalInfoRequested.count == 1` and `applicationId` == the application.
  2. `Reply_StoresReply_ClearsMarker_VisibleToBoard` (AS2): the resident uploads a reply attachment and replies "Survey attached". Then:
     - the DB `RespondedAt`, `ResponseMessage` and `RespondedByUserId` are set;
     - the resident status is back to `Submitted`, and the dashboard count is 0;
     - the board list item has `infoRequested == false`;
     - the board detail (027 endpoint, as board member **and** as manager) shows `responseMessage == "Survey attached"`, and its attachments include the reply file with `infoRequestId` set.
  3. `InfoRequest_DoesNotMoveDueDate` (AS3): `DueDate` is identical before the request, while outstanding, and after the reply.
  4. `Reply_Twice_Refused`: 409 `INFO_ALREADY_ANSWERED`, and the response is unchanged.
  5. `Reply_Blank_Refused`: 422 `VALIDATION_ERROR`.
  6. `Reply_OnClosedApplication_Refused`: 409 `APPLICATION_CLOSED`.
  7. `ReplyAttachment_DisallowedType_Refused`: 422 `UNSUPPORTED_FILE_TYPE`.
- [X] T047 [P] [US3] Add `HOAManagementCompany.Tests/Integration/Dashboard/DashboardArchitecturalAlertTests.cs` (built on `ResidentArcTestBase`, since the alert needs a resident with ARC data; the existing `DashboardTests.cs` uses the shared seed resident): `architecturalInfoRequested.count` is 0 with no ARC data, counts only the active property's Open applications with an unanswered request, and ignores other properties.

### Implementation

- [X] T048 [US3] Create `ReplyInfoRequestEndpoint.cs` (under `ArcLocks.InLockedTransactionAsync`: requires `Status == Open` and `RespondedAt == null`; sets the reply fields and `RespondedAt` from `TimeProvider`; logs `ArcInfoReplied`) and `UploadReplyAttachmentEndpoint.cs` (the same validator; counts against the application's existing attachments; key `arc/{communityId}/{applicationNumber}/{guid}`; `InfoRequestId` and `UploadedByUserId` set) in `HOAManagementCompany/Features/Property/Architectural/`
- [X] T049 [US3] In the 027 code:
  - Add `ResponseMessage` and `RespondedAt` to the board info-request DTO in `HOAManagementCompany/Features/Board/Architectural/ArcModels.cs`, and add `InfoRequestId` to the board attachment DTO.
  - Populate both in `ArcQueries.BuildDetailAsync`.
  - Extend `HOAManagementCompany.Tests/Integration/Board/Architectural/ApplicationDetailEndpointTests.cs` with a case asserting the reply fields appear for board members.
- [X] T050 [US3] Add `ArchitecturalInfoRequestedSummary(int Count, Guid? ApplicationId)` to `HOAManagementCompany/Features/Dashboard/Models/` and the `DashboardResponse` record. Compute it in `DashboardService.GetDashboardAsync`: `Open` applications for `propertyId` with any `InfoRequests.RespondedAt == null`, oldest first.
- [X] T051 [P] [US3] Create `info-reply.component.ts`: it shows each unanswered question, has a reply textarea (labeled, required, 2000 max with a counter) and an optional file upload through the reply-attachment endpoint, and sends the reply. Embed it in `request-detail.component.ts` when the status is `MoreInfoRequested`. Add `info-reply.component.spec.ts` (ATL: blank is blocked; a successful reply refreshes the detail; 409 shows its message). All in `neko-hoa/src/app/features/property/architectural/`. The layout MUST be responsive (FR-027): one column under 600 px, tables become stacked cards on phones, and no fixed width may overflow 375 px.
- [X] T052 [P] [US3] Add a dashboard alert to `neko-hoa/src/app/features/dashboard/dashboard.component.ts`: "The board needs more information about your architectural request" with a link to `/app/property/architectural/{applicationId}`, shown when `count > 0` with `role="status"`. Add the field to the dashboard model in `core/services/dashboard.service.ts`. Extend `dashboard.component.spec.ts`: the alert shows for count 1 and is absent for 0.
- [X] T053 [P] [US3] Show the resident's reply and reply attachments in the board detail panel. In `neko-hoa/src/app/features/board/architectural/application-detail-panel.component.ts`, render "Owner replied: …" under each answered info request. Extend `application-detail-panel.component.spec.ts`.

**Checkpoint**: T046, T047 and T049's board test pass.

---

## Phase 7: User Story 4 — Withdraw a request (P3)

**Goal**: An undecided request becomes Withdrawn. It is hidden from the board by default but kept, and shown when the board filters for it.
**Independent test**: withdraw through the resident API, then query the board list with and without `includeWithdrawn`.

### Tests (write first)

- [X] T054 [P] [US4] Create `HOAManagementCompany.Tests/Integration/Property/Architectural/WithdrawTests.cs`:
  1. `Withdraw_Undecided_ClosesAsWithdrawn_HiddenFromBoardByDefault` (AS1). Assert:
     - 200 with `status == "Withdrawn"`;
     - the DB has `Status == Closed`, `DecisionOutcome == Withdrawn`, and `WithdrawnAt`/`WithdrawnByUserId` set;
     - the board `?status=open` doesn't contain it, and neither does `?status=closed`;
     - `?status=closed&includeWithdrawn=true` contains it with `decision.outcome == "Withdrawn"`;
     - `counts.closed` excludes it by default and includes it with the flag;
     - no outbox row was written.
  2. `Withdraw_DecisionReached_Refused` (AS2): 409 `APPLICATION_DECIDED`; the state is unchanged.
  3. `Withdraw_Closed_Refused`, a `[Theory]` over Approved, Denied and already-Withdrawn (AS2): 409 `APPLICATION_CLOSED`; the state is unchanged.
  4. `Withdraw_RacingDecidingVote_ExactlyOneWins`: on a 3-member board with 1 approve vote, fire the withdraw and the deciding approve vote concurrently. The final state is either Withdrawn (and the vote got 409 `APPLICATION_CLOSED`) or DecisionReached (and the withdraw got 409 `APPLICATION_DECIDED`), never both.
  5. `Sweep_IgnoresWithdrawn`: a withdrawn application past its due date gets no lapse or reminder (027 `ArcSweepService` via the job endpoint).
- [X] T055 [P] [US4] Extend `HOAManagementCompany.Tests/Integration/Board/Architectural/ApplicationsListEndpointTests.cs`: `includeWithdrawn` is optional, and omitting it leaves the previous behavior for non-withdrawn data unchanged (regression).

### Implementation

- [X] T056 [US4] Create `WithdrawApplicationEndpoint.cs` in `HOAManagementCompany/Features/Property/Architectural/`. Under `ArcLocks.InLockedTransactionAsync`:
  - `DecisionReached` → `APPLICATION_DECIDED`; `Closed` → `APPLICATION_CLOSED`.
  - Otherwise set `Status = Closed` and `DecisionOutcome = Withdrawn`; set `WithdrawnAt`/`ClosedAt` to now and `WithdrawnByUserId`/`ClosedByUserId` to the caller.
  - Log `ArcWithdrawn`; send no outbox row.
- [X] T057 [US4] Add `bool? IncludeWithdrawn` to the request in `HOAManagementCompany/Features/Board/Architectural/ApplicationsListEndpoint.cs`. When it is not true, the `closed` filter and `counts.closed` exclude `DecisionOutcome == ArcOutcome.Withdrawn`. Withdrawn rows are `Closed`, so the open tab is already unaffected. Update the 027 contract doc `specs/027-board-arc-review/contracts/architectural-applications.md` with the new parameter.
- [X] T058 [P] [US4] Add a "Show withdrawn" toggle on the Closed tab in `neko-hoa/src/app/features/board/architectural/applications-page.component.ts`. It passes `includeWithdrawn=true` through `core/services/architectural.service.ts`, and the row shows "Withdrawn". Extend `applications-page.component.spec.ts` and `architectural.service.spec.ts`.
- [X] T059 [P] [US4] Create `withdraw-dialog.component.ts`: an accessible confirm dialog (focus trap, Escape cancels, explains that it can't be undone) that calls `withdraw` and refreshes the detail. Wire it into `request-detail.component.ts`. Add `withdraw-dialog.component.spec.ts`. All in `neko-hoa/src/app/features/property/architectural/`. The layout MUST be responsive (FR-027): one column under 600 px, tables become stacked cards on phones, and no fixed width may overflow 375 px.

**Checkpoint**: T054 and T055 pass.

---

## Phase 8: User Story 6 — Revise and resubmit after a denial (P3)

**Goal**: From a denied request, create a pre-filled revision draft with carried-over attachments, edit it, and submit it as `ARC-<n>` v2.
**Independent test**: seed a Closed/Denied application with 2 attachments, then revise, remove one carried file, add one, and submit.

### Tests (write first)

- [X] T060 [P] [US6] Create `HOAManagementCompany.Tests/Integration/Property/Architectural/ReviseTests.cs`:
  1. `Revise_Denied_CreatesPrefilledDraftWithCarriedAttachments` (AS1): for a seeded `ARC-N` v1 that is Closed/Denied with 2 attachments, revise returns 201. The draft's `previousRevisionId` == v1, its fields equal v1's, and `carriedAttachments` lists both files.
  2. `RemoveCarriedAttachment_DoesNotTouchEarlierRevision` (AS2): PUT `removedCarriedAttachmentIds` with one ID and submit. v1 still has 2 attachment rows and both objects exist; v2 has 1 carried attachment plus any new upload.
  3. `SubmitRevision_CreatesOpenV2WithNewDatesAndNoVotes` (AS3): v2 has the same `ApplicationNumber`, `Revision == 2`, `PreviousRevisionId == v1`, `Status == Open`, `ReceivedDate` == today, a `DueDate` computed from the current settings, and 0 votes. The resident detail `revisions` lists v1 and v2. 027's board detail shows the v2 revision history, and the board list shows v2 as Open.
  4. `Revise_NotDenied_Refused` (AS4), a `[Theory]` over Open, DecisionReached, Approved and Withdrawn: 409 `REVISION_NOT_ALLOWED`; no draft is created.
  5. `Revise_Twice_RefusedWhileDraftExists`: the second revise gets 409 `REVISION_NOT_ALLOWED`.
  6. `Revise_OlderRevision_Refused`: when v2 exists, revising v1 gets 409.
  7. `Revise_CountLimitIncludesCarriedFiles`: with `MaxFilesPerApplication = 3` and 2 carried files, one upload is accepted and the next gets 422 `ATTACHMENT_LIMIT_REACHED`.

### Implementation

- [X] T061 [US6] Create `ReviseApplicationEndpoint.cs`. It checks that the application is the latest revision, is Closed with `DecisionOutcome == Denied`, and has no draft with that `PreviousRevisionId` (all → 409 `REVISION_NOT_ALLOWED`). It then creates the draft, pre-filled from the application, and logs `ArcRevisionDraftCreated`. In `ResidentArcDraftService` (`HOAManagementCompany/Features/Property/Architectural/`), add `carriedAttachments` (previous revision's attachments minus the removed IDs) to the draft GET, include carried files in the limit counts in `UploadDraftAttachmentEndpoint`, and let `DraftAttachmentUrlEndpoint` serve carried attachments.
- [X] T062 [P] [US6] Add revise mode to `request-form.component.ts`:
  - The `property/architectural/:id/revise` route calls `revise(id)`, or reopens the existing revision draft, and loads it.
  - Carried attachments get "Remove" buttons, which update `removedCarriedAttachmentIds`.
  - A "Revision of ARC-N" banner is shown.

  Add a "Revise and resubmit" button to `request-detail.component.ts` (shown when `canRevise`). Extend both specs.

**Checkpoint**: T060 passes; the denial → resubmit loop with 027 is closed.

---

## Phase 9: Polish & cross-cutting

- [X] T063 [P] Add `ResidentArcTelemetryHygieneTests` in `HOAManagementCompany.Tests/Integration/Property/Architectural/ResidentArcTelemetryHygieneTests.cs`, modeled on `ArcTelemetryHygieneTests.cs`. Across a submit, upload, reply and withdraw flow, no log or span may contain the file name, the owner name, a storage key, or the reply or description text.
- [X] T064 [P] Add `ResidentArcScopeStaticAnalysisTests` in `HOAManagementCompany.Tests/Integration/Property/Architectural/ResidentArcScopeStaticAnalysisTests.cs`. Every `*Endpoint.cs` in `Features/Property/Architectural/` must call `RequirePropertyId()` and use `ResidentArcScope`, and none may reference `ICommunityScopeResolver` (resident scope only, FR-023).
- [X] T065 [P] Extend `HOAManagementCompany/Seed/ArchitecturalSeeder.cs`, keeping it idempotent and Dev-only: give the seeded resident's property one Open application with an unanswered info request, and one Closed/Denied application, so the quickstart's US3 and US6 steps work. Extend the seeder test if one asserts counts.
- [X] T066 [P] Make sure Storybook stories exist for `my-requests-page`, `request-detail`, `request-form`, `info-reply` and `withdraw-dialog` (`*.stories.ts`, built from fixtures in `neko-hoa/src/app/features/property/architectural/resident-arc-fixtures.ts`)
- [X] T067 [P] Add the responsive check (FR-027) as Playwright spec `neko-hoa/e2e/resident-arc-responsive.spec.ts`. At 375×812, 768×1024 and 1280×800 it visits the my-requests list, the new-request form with one uploaded file, a request detail with an info request, and the withdraw dialog. It asserts `document.documentElement.scrollWidth <= window.innerWidth` and that Save draft, Submit, Reply and Withdraw are visible and clickable.
- [X] T068 [P] Add the rate-limit test as `HOAManagementCompany.Tests/Integration/RateLimiting/ResidentWritesRateLimitTests.cs`, modeled on `RateLimitingIsolationTests.cs`. With `RateLimiting:ResidentWritesPermitsPerMinute=2`, a resident's third `POST /property/architectural-applications/drafts` in the window returns 429, a second resident is unaffected, and `GET /property/architectural-applications` (a read) is never limited.
- [X] T069 Add Repowise markers to the files listed in plan.md §Repowise (`ResidentArcDraftService`/`ResidentArcSubmitService`/`ResidentArcQueries` `domain=resident-arc`; `ArcAttachmentValidator` `domain=resident-arc-uploads`; `ArcUploadOptions` `domain=configuration`; `spec.md` `section=summary`)
- [X] T070 Bring `specs/029-resident-arc-requests/spec.md`, `quickstart.md`, `contracts/` and `data-model.md` into line with what was built (no drift), and mark completed tasks `[X]` in this file
- [X] T071 Run the full gates: `dotnet build`; `dotnet test`; in `neko-hoa`, `npm run test:ci` and `npm run build`; `npx playwright test e2e/resident-arc-attachments.spec.ts`; `npm run e2e:ci` where the environment allows. Fix any failures and record any that can't run locally, with the reason.

---

## Spec → test traceability (CLAUDE.md NLT rule)

| Spec NLT | Test |
|---|---|
| US1 Independent Test | `FileRequestTests.Submit_DraftWithAttachment_CarriesAttachmentToApplication` + Cypress `resident-arc-submit.cy.ts` |
| US1 AS1–AS8 | `FileRequestTests` tests 1–8 (T025), in order |
| US2 Independent Test | `TrackRequestsTests` (T039) |
| US2 AS1–AS5 | `TrackRequestsTests` tests 1–5 |
| US3 Independent Test | `InfoReplyTests.Reply_StoresReply_ClearsMarker_VisibleToBoard` |
| US3 AS1–AS3 | `InfoReplyTests` tests 1–3 (T046) |
| US4 Independent Test | `WithdrawTests` tests 1–3 |
| US4 AS1–AS2 | `WithdrawTests` tests 1, 2–3 (T054) |
| US5 Independent Test | `AttachmentTests` (T035) + Playwright `resident-arc-attachments.spec.ts` |
| US5 AS1–AS4 | `AttachmentTests` tests 1–4 |
| US6 Independent Test | `ReviseTests` tests 1–3 |
| US6 AS1–AS4 | `ReviseTests` tests 1–4 (T060) |
| Edge: non-owner / board-member / cross-community / co-owner | `ResidentArcAuthorizationTests` (T040) |
| Edge: concurrent submits | `FileRequestTests.ConcurrentSubmits_GetDistinctSequentialNumbers` |
| Edge: delete draft with attachments | `FileRequestTests.DeleteDraft_RemovesDraftAndItsObjects` |
| Edge: expired link | `AttachmentLinkExpiryTests.ExpiredLink_StopsWorking_AndANewRequestIssuesAWorkingLink` (+ `AttachmentTests.AttachmentUrl_IsShortLived_AndNeverEmbedded`) |
| Edge: storage unavailable during upload | `AttachmentStorageFailureTests.Upload_StorageUnavailable_FailsCleanlyAndCanRetry` |
| Edge / FR-008: submitted content not editable | `TrackRequestsTests.SubmittedApplication_CannotBeEdited` |
| SC-007: in the list within 5 s | `FileRequestTests.Submit_AppearsInBoardOpenList` (resident list + stopwatch) |
| FR-027: responsive | Playwright `resident-arc-responsive.spec.ts` |
| Constitution §7: rate limit | `ResidentWritesRateLimitTests` |

---

## Dependencies & execution order

- **Setup (T001–T004)** → **Foundational (T005–T024)** → user stories.
- **US1 (Phase 3)** and **US5 (Phase 4)** are both P1. US5's upload endpoint needs US1's draft endpoints (T030), so do US1 first, then US5. Together they are the MVP.
- **US2 (Phase 5)** needs only Foundational. Its tests seed state directly, so it can run in parallel with US1 and US5 once T024 is done.
- **US3 (Phase 6)**, **US4 (Phase 7)** and **US6 (Phase 8)** each need Foundational plus `ResidentArcQueries` (T041) for detail responses. US6 also needs US1's submit service (T029) and US5's upload endpoint (T036).
- Within a story: tests → services → endpoints → frontend.
- **Polish (Phase 9)** comes last; T071 is the final gate.

## Parallel examples

- Foundational: T006, T007 and T008 together; T015, T017, T019, T021, T022 and T023 together after T009.
- US1: T025 and T026 together (tests); then T032 and T033 alongside T028–T030.
- US2: T039 and T040 together; T043 and T044 together.
- US3: T046 and T047 together; T051, T052 and T053 together.

## Implementation strategy

1. **MVP**: Phases 1–4. A resident can file, attach and submit; the board sees it.
2. Add US2 (tracking) and ship.
3. Add US3, US4 and US6 in any order (all P3).
4. Polish, then the full gates (T071).
