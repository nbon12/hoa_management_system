# Research: Dashboard and Violations Summary (001)

**Feature**: 001-dashboard-violations-summary  
**Date**: 2025-03-14

## 1. Dashboard and violation-count API pattern

**Decision**: Expose a dedicated dashboard summary endpoint (e.g. `GET /api/dashboard/summary` or `GET /api/dashboard`) that returns, for the authenticated user, at least `openViolationCount`. Other boxes (current balance, work orders, architecture requests) return placeholder values. Frontend calls this once after login to render the grid; Violations box shows loading state until the response is received.

**Rationale**: Single round-trip for dashboard data; backend enforces user context (no user id in URL). Aligns with stateless API and keeps count and list consistent (same scoping logic).

**Alternatives considered**: Separate endpoint per box (more round-trips, rejected); client-side aggregation of violation list to derive count (wasteful and duplicates list-page logic, rejected).

---

## 2. User–property ownership and violation scoping

**Decision**: Introduce a **Property** (or **Unit**/Lot) entity and a **user–property ownership** association so that "violations across all properties the user owns" is well-defined. Add **Violation.PropertyId** (required FK to Property). Count and list APIs filter violations by: `Status == Open` AND `Violation.PropertyId` in the set of property IDs owned by the current user. Ownership: either a join table (UserProperty) or a column on Property (e.g. OwnerUserId). Prefer **Property.OwnerUserId** (string, FK to Identity User) if each property has a single owner; use **UserProperty** (UserId, PropertyId) if many-to-many or multiple owners per property. For HOA, one owner per property is common—Property.OwnerUserId is chosen unless product requires multi-owner.

**Rationale**: Spec requires "all properties the user owns"; current Violation model has no property or user association. Property + ownership model is standard for HOA/associations and supports future features (balance per property, work orders per property).

**Alternatives considered**: Violation.CreatedBy as "owner" (wrong—creator may be manager, not homeowner; rejected). Violation.UserId only (no property)—cannot support "multiple properties" (rejected).

---

## 3. Frontend technology (Angular vs Blazor)

**Decision**: Implement Dashboard and My Violations UI in **Angular** in a dedicated app at **`frontend/`** at the repository root for this feature. Project has directed use of Angular (per constitution); the frontend directory is the canonical Angular app location.

**Rationale**: Constitution requires Frontend: Angular. Project direction for 001 is to use Angular in `frontend/`; this aligns with the constitution and with tasks.md.

**Alternatives considered**: Using existing Blazor Components (rejected—project chose Angular for this feature); blocking feature until Angular (rejected—Angular app is in scope for 001).

---

## 4. My Violations list API

**Decision**: Expose a paginated list endpoint, e.g. `GET /api/violations/mine?limit=&offset=`, returning only open violations for properties owned by the current user. Response includes total count (or a separate count endpoint) so the dashboard count and list total can be validated (SC-003). Enforce pagination per constitution (limit/offset).

**Rationale**: Aligns with FR-005, FR-010, and pagination requirement; supports large result sets and consistent UX.

**Alternatives considered**: Unpaginated list (rejected per constitution); count-only endpoint separate from list (acceptable—dashboard can call count only to avoid loading full list).

---

## 5. Loading and error states (Violations box)

**Decision**: Backend returns 200 with count (or list); on failure return 4xx/5xx with a clear, non-technical message body. Frontend shows a loading indicator (spinner/skeleton) until the dashboard summary response is received; if the violations count fails, show a user-friendly message and leave the rest of the dashboard usable (FR-008).

**Rationale**: Matches spec FR-007, FR-008 and clarification (loading indicator, no blocking).

**Alternatives considered**: Show 0 on error (misleading; rejected). Block entire dashboard on count failure (rejected per spec).
