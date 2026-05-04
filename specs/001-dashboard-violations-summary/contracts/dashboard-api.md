# Dashboard API Contract

**Feature**: 001-dashboard-violations-summary  
**Audience**: Backend implementers, frontend consumers, test authors

## Authentication

All endpoints require an authenticated user (e.g. Okta/Identity). The current user's identity is used to scope data; no user id in path or query for dashboard or my-violations.

---

## GET Dashboard summary

Returns summary data for the four dashboard boxes. For 001, only the violations count is real; other boxes are placeholders.

**Request**

- **Method**: GET
- **Path**: `/api/dashboard/summary` (or `/api/dashboard` — align with existing API style)
- **Headers**: Authorization (Bearer or cookie per existing auth)
- **Query**: None

**Response (200 OK)**

- **Content-Type**: application/json
- **Body** (example):

```json
{
  "openViolationCount": 2,
  "currentBalance": null,
  "workOrdersCount": null,
  "architectureRequestsCount": null
}
```

- **Semantics**:
  - `openViolationCount`: Count of open violations across all properties owned by the current user. Integer ≥ 0.
  - `currentBalance`, `workOrdersCount`, `architectureRequestsCount`: Placeholder; null or a display string for 001. Frontend shows placeholder text when null.

**Errors**

- **401 Unauthorized**: User not authenticated. Frontend redirects to login.
- **500 Internal Server Error**: Body should include a non-technical, user-friendly message (per FR-008). Use the message **"Failed to load violation count"**. Frontend shows this message and leaves rest of dashboard usable.

**Scoping**

- Count MUST be computed only for the current user (properties where OwnerUserId = current user id). No cross-user data (FR-010).

---

## Loading and error behavior (frontend)

- Until the dashboard summary response is received: show loading indicator in Violations box; box not clickable (FR-007).
- On success: show count; make count clickable (navigate to My Violations).
- On failure: show the message **"Failed to load violation count"**; do not block other boxes (FR-008).
