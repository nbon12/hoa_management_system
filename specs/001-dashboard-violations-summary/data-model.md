# Data Model: Dashboard and Violations Summary (001)

**Feature**: 001-dashboard-violations-summary  
**Date**: 2025-03-14

## Scope

This document describes entities and relationships required to support the Dashboard (violation count) and My Violations list, including changes to the existing model.

## Existing entities (unchanged or extended)

### User (Identity)

- **Source**: ASP.NET Core Identity (`IdentityUser`).
- **Identification**: `Id` (string).
- **Relevant to feature**: Current user identity drives dashboard and list scoping; roles (Homeowner, Board Member) already exist per constants.

### ViolationType

- **Source**: Existing `ViolationType` model.
- **Relevant to feature**: Referenced by Violation; no schema change.

### Violation (extended)

- **Source**: Existing `Violation` model.
- **Current attributes**: Id, Description, Status (Open/Closed), OccurrenceDate, ViolationTypeId, ViolationType, plus audit (CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted).
- **Change for 001**: Add **PropertyId** (Guid, required, FK to Property). A violation is associated with a single property.
- **Validation**: Status is Open or Closed; for dashboard and My Violations only Open violations are included. Violations are scoped to properties owned by the current user (via Property.OwnerUserId).

---

## New entity: Property

- **Purpose**: Represents a property (unit/lot) that can have violations and that is owned by a user. Supports "violations across all properties the user owns."
- **Attributes**:
  - **Id**: Guid, PK.
  - **OwnerUserId**: string, required, FK to AspNetUsers.Id. The user who owns this property (homeowner).
  - **DisplayName** (or **Address** / **Name**): string, for display (e.g. in list or future features).
  - Optional: audit fields (CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted) if Property is auditable.
- **Relationships**:
  - One Owner (IdentityUser) to many Properties.
  - One Property to many Violations (Violation.PropertyId).
- **Uniqueness**: At least Id; business uniqueness (e.g. one property per address) is out of scope for 001 unless required by product.
- **State**: No lifecycle state required for 001; active vs inactive can be added later.

---

## Relationships summary

| From       | To           | Cardinality | How |
|------------|--------------|-------------|-----|
| User       | Property     | 1 : N       | Property.OwnerUserId |
| Property   | Violation    | 1 : N       | Violation.PropertyId |
| Violation  | ViolationType| N : 1       | Violation.ViolationTypeId (existing) |

## Query rules for feature

- **Dashboard open violation count**: Count of Violations where `Status == Open` AND `Violation.Property.OwnerUserId == currentUserId` (and not soft-deleted).
- **My Violations list**: Same filter; return list with pagination (limit/offset); include Property and ViolationType for display.
- **Data isolation (FR-010)**: All queries MUST filter by current user (via Property ownership); never expose another user's count or violation records.

## State transitions

- **Violation.Status**: Open ↔ Closed. For 001 only Open is used in count and list; transitions (e.g. closing a violation) are out of scope unless already implemented elsewhere.

## Migration notes

- Add `Property` table and `Violation.PropertyId` (required FK to Property).
- **Existing Violations**: There is no production database at this time; we are in early development. Any existing Violation rows can be erased (e.g. table truncated or data cleared) before or as part of the migration. No backfill of PropertyId is required.
