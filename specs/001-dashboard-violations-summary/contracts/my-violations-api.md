# My Violations API Contract

**Feature**: 001-dashboard-violations-summary  
**Audience**: Backend implementers, frontend consumers, test authors

## Authentication

All endpoints require an authenticated user. Data is scoped to the current user's properties only (FR-005, FR-010).

---

## GET My Violations (paginated list)

Returns open violations for all properties owned by the current user. Supports pagination per constitution.

**Request**

- **Method**: GET
- **Path**: `/api/violations/mine` (or `/api/violations/me` — align with existing API style)
- **Headers**: Authorization (Bearer or cookie per existing auth)
- **Query**:
  - `limit` (optional): number of items per page; default and max per API policy (e.g. 10, 50).
  - `offset` (optional): number of items to skip; default 0.

**Response (200 OK)**

- **Content-Type**: application/json
- **Body** (example):

```json
{
  "items": [
    {
      "id": "guid",
      "description": "string",
      "occurrenceDate": "ISO8601",
      "violationTypeName": "string",
      "propertyDisplayName": "string"
    }
  ],
  "totalCount": 42
}
```

- **Semantics**:
  - `items`: Open violations for the current user's properties only; order by occurrence date descending (or per product).
  - `totalCount`: Total number of open violations for the current user (across all pages). Enables dashboard count validation (SC-003) and pagination UI.
- **Filtering**: Only violations where Status = Open and Violation.Property.OwnerUserId = current user id (and not soft-deleted).

**Errors**

- **401 Unauthorized**: User not authenticated.
- **500 Internal Server Error**: Body with user-friendly message.

**Scoping**

- MUST NOT return violations for other users' properties (FR-010).

---

## Optional: GET My Violations count only

If the dashboard uses a dedicated count endpoint instead of the summary endpoint, document here:

- **Method**: GET
- **Path**: e.g. `/api/violations/mine/count`
- **Response**: `{ "count": 42 }` with same scoping rules.

If count is only provided via the dashboard summary, this section can be omitted.
