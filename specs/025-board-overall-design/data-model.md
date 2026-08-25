# Phase 1 Data Model: Board Member Experience — Overall Design

## New entities

### `Community`

The tenant boundary for all board-side features. Replaces the denormalized
`Property.CommunityId` / `Property.CommunityName` string pair.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key. |
| `LegalName` | `string` | Nullable at migration time; backfilled empty, filled in later by a community manager (no UI in this spec). |
| `CommunityName` | `string` | The management company's human-readable handle. **Unique** (see research.md R1). Backfilled from `Property.CommunityName`. |
| `County` | `string?` | Nullable. |
| `FormationDate` | `DateOnly?` | Nullable. |
| `ManagementStartDate` | `DateOnly?` | Nullable. |
| `Description` | `string?` | Nullable, free text. |
| `Status` | `CommunityStatus` | `Active` \| `Inactive`. New rows default `Active`. |
| `ParentCommunityId` | `Guid?` | Self-referencing FK. Null for a master association or a standalone community; set for a sub-association (FR-003). |
| `CreatedAt` | `DateTimeOffset` | `DateTimeOffset.UtcNow` default, matching existing entity convention (`Property.CreatedAt`, `Violation.CreatedAt`). |

Navigation: `ParentCommunity` (`Community?`), `SubCommunities` (`ICollection<Community>`),
`Properties` (`ICollection<Property>`), `Memberships` (`ICollection<CommunityMembership>`).

### `CommunityMembership`

The sole source of board-side authorization (FR-007, FR-012). Deliberately separate from
`UserProperty` — property ownership and board membership are independent (FR-011).

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key. |
| `UserId` | `string` | FK to `AspNetUsers.Id` (matches `IdentityUser.Id` type used by `UserProperty.UserId`). |
| `CommunityId` | `Guid` | FK to `Community.Id`. |
| `Role` | `CommunityRole` | See enum below. |
| `Status` | `MembershipStatus` | `Active` \| `Inactive`. |
| `StartDate` | `DateOnly` | Required. |
| `EndDate` | `DateOnly?` | Nullable — an open-ended membership has no end date (FR-007). |
| `CreatedAt` | `DateTimeOffset` | `DateTimeOffset.UtcNow` default. |

Index: unique on `(UserId, CommunityId, Role)` — a user cannot hold the same role twice in the
same community; they *can* hold two different roles in the same community (e.g. Board Member and
Accountant), each as a separate row. The resolver evaluates the rows together and grants the
**union** of their capabilities — allow if any active row confers the requested capability (spec
Clarifications 2026-08-23; research.md R4). Scope stays per `Community` row: neither row is
widened across the master/sub-association hierarchy.

**Effective-permission rule** (FR-010): a membership confers no permission when
`Status != Active`, or `EndDate` is non-null and `EndDate < today (UTC)`. This is evaluated at
read time by `ICommunityScopeResolver` (research.md R4) — never cached or baked into the JWT
(FR-014).

### `CommunityRole` (enum)

```csharp
public enum CommunityRole
{
    Resident,
    BoardMember,
    CommunityManager,
    Accountant,
}
```

No `CommitteeMember` value — architectural review is a `BoardMember` capability (spec
Clarifications, 2026-08-20). `Resident` exists in this enum for completeness/future use by sibling
specs; this spec's own authorization surface is driven by `UserProperty` for resident access
(FR-015, unchanged) and by `CommunityMembership` for every non-resident role.

### `MembershipStatus` (enum)

```csharp
public enum MembershipStatus
{
    Active,
    Inactive,
}
```

### `CommunityStatus` (enum)

```csharp
public enum CommunityStatus
{
    Active,
    Inactive,
}
```

An inactive `Community` makes every membership in it non-board-accessible regardless of the
membership's own status (spec Edge Cases: "A user holds exactly one community but it is
inactive/offboarded").

### `MetricDescriptor` (code-level contract, not persisted)

FR-029 requires "a single metric descriptor contract," and SC-004 requires adding a metric to
require *only* a descriptor-collection change — no schema/migration. This is therefore a plain
C# record, not a database table:

```csharp
public sealed record MetricDescriptor(
    string Id,                        // stable, e.g. "over-60-days-delinquent"
    MetricSurface Surface,            // which page/table renders it
    string Label,
    string DefinitionText,            // glossary content — same source as the row (FR-034)
    Func<MetricContext, Task<MetricValue>> Resolve, // computes Value/Detail/Status per request
    MetricEmphasis Emphasis,
    CommunityCapability RequiredCapability  // FR-035: not rendered if the caller lacks this capability
);
```

Concrete `MetricDescriptor` instances (the actual metrics — delinquency %, ACH registration %,
etc.) are **spec 2's** responsibility (Community Overview & Metrics); this spec ships the
`MetricDescriptor` type, the registry/DI registration mechanism (`IEnumerable<MetricDescriptor>`
resolved from DI, per FR-030), and the endpoint that serves it (see `contracts/`), with zero
concrete descriptors registered yet — an empty registry is a valid state per the spec's Edge
Cases ("a community has no metrics configured → explicit empty state").

## Modified entities

### `Property`

- **Remove**: `CommunityId` (string), `CommunityName` (string).
- **Add**: `CommunityId` (`Guid`, FK to `Community.Id`).
- **Add navigation**: `Community` (`Community`).
- `CommunityName` for API responses is now read via `property.Community.CommunityName`
  (FR-004 — "derived from the related community rather than stored separately").

### `Violation`

- **Remove**: `CommunityId` (string).
- **Add**: `CommunityId` (`Guid`, FK to `Community.Id`) — set from `Property.CommunityId` at
  migration time (FR-006).

### `ApplicationUser`

- **Add**: `Memberships` (`ICollection<CommunityMembership>`) navigation property.
- **Add**: `LastActiveMode` (`UserMode` enum: `Resident` \| `Board`; default `Resident`) — see
  research.md R7.
- Update the existing Repowise marker comment (`ApplicationUser.cs:5-7`) to mention the new
  collection and field once implemented, per constitution §2 ("Repowise... Pull requests MUST
  include regenerated or updated Repowise outputs in those regions").

## Relationships

```text
Community 1──* Property            (Property.CommunityId FK)
Community 1──* Violation           (Violation.CommunityId FK)
Community 1──* CommunityMembership (CommunityMembership.CommunityId FK)
Community 0..1──* Community        (self-referencing ParentCommunityId — master/sub)
ApplicationUser 1──* CommunityMembership (CommunityMembership.UserId FK)
ApplicationUser 1──* UserProperty        (unchanged — resident/property-ownership path)
```

`CommunityMembership` and `UserProperty` are deliberately two separate join tables with no FK
between them — a user's board membership and their property ownership are independent axes
(FR-011), and a membership can exist for a user who owns no property in the community at all
(e.g. an accountant or an off-site board member).

## Migration sequencing (single EF Core migration, per research.md R5)

1. Create `Community`, `CommunityMembership` tables and the three new enums.
2. Add nullable `Property.CommunityId` (Guid) and `Violation.CommunityId` (Guid) columns
   alongside the existing string columns (temporary dual-write window within the same migration's
   data step, not a separate deploy).
3. Data step: backfill `Community` rows from distinct existing
   `(Property.CommunityId, Property.CommunityName)` pairs; set the new Guid FK columns on
   `Property` and `Violation` from the matching `Community.Id`.
4. Drop the old string `CommunityId` / `CommunityName` columns; make the new Guid FK columns
   non-nullable; add the FK constraints and the `Community.CommunityName` unique index.
5. Add `ApplicationUser.LastActiveMode` (default `Resident`).

Reversible per FR-005: the down-migration re-derives the string columns from the `Community` row
being referenced before dropping the new schema.
