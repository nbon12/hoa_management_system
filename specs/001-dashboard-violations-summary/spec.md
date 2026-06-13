# Feature Specification: Login Dashboard with Violations Summary

**Feature Branch**: `001-dashboard-violations-summary`  
**Created**: 2025-03-14  
**Status**: Implemented  
**Input**: User description: "As a homeowner and/or board member (who MAY also be a homeowner) I want to be able to login and see the Dashboard when I login. Upon logging in, I should see four boxes. One of these boxes should show me the count of violations I have, and if I click that number, it should take me to the 'My Violations' page, which shows any violations associated to me."

## Clarifications

### Session 2025-03-14

- Q: Which violations are included in the dashboard count (all on property vs. only open/active)? → A: Count only open violations; a separate (future) way to see closed ones.
- Q: What should the Violations box show while the count is loading? → A: Show a loading indicator (e.g. spinner or skeleton) until the count is available.
- Q: Display order of the four boxes? → A: Grid: two boxes on top, two on bottom; left to right in listed order (Current Balance, Violations, Work Orders, Architecture Requests).
- Q: Explicit requirement that count and list are strictly scoped to the current user (no cross-user visibility)? → A: Yes, add explicit functional requirement.
- Q: If user has multiple properties, scope of count and list? → A: All properties: count and list aggregate open violations across all properties the user owns.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View Dashboard After Login (Priority: P1)

As a homeowner or board member, after I log in, I am taken to a Dashboard that shows four summary boxes: Current Balance, Violations, Work Orders, and Architecture Requests, so that I can see key information at a glance.

**Why this priority**: The dashboard is the primary landing experience after login; without it, users have no clear starting point.

**Independent Test**: Can be fully tested by logging in and confirming the dashboard is displayed with four visible summary boxes, and delivers immediate orientation for the user.

**Acceptance Scenarios**:

1. **Given** I am a logged-in homeowner or board member, **When** I navigate to or land on the dashboard, **Then** I see a page with four distinct summary boxes.
2. **Given** I have just completed login, **When** the login succeeds, **Then** I am shown the Dashboard (not another page) as the default destination.

---

### User Story 2 - See My Violation Count and Open My Violations (Priority: P1)

As a homeowner or board member, I see one of the four dashboard boxes showing the count of open violations across all properties I own; when I click that count, I am taken to the "My Violations" page where I can see the list of those open violations.

**Why this priority**: This is the only box behavior specified in the feature; it is core to the requested value.

**Independent Test**: Can be fully tested by logging in, reading the open violation count in the box, clicking the number, and confirming the My Violations page opens and shows the same set of open violations (or an empty list when count is zero).

**Acceptance Scenarios**:

1. **Given** I am on the Dashboard, **When** I look at the violations summary box, **Then** I see a number equal to the count of open violations across all properties I own.
2. **Given** I am on the Dashboard and my open violation count is zero, **When** I look at the violations box, **Then** I see the number 0 (or an equivalent "no violations" indication).
3. **Given** I am on the Dashboard, **When** I click the violation count in that box, **Then** I am taken to the "My Violations" page.
4. **Given** I am on the Dashboard and the violation count is still loading, **When** I view the Violations box, **Then** I see a loading indicator (e.g. spinner or skeleton) and the box is not clickable until the count is shown.
5. **Given** I am on the My Violations page, **When** I view the page, **Then** I see only open violations across all properties I own (homeowner view), whether I am a homeowner or board member. (A future feature may allow viewing closed violations.)

---

### User Story 3 - Other Dashboard Boxes (Priority: P2)

As a homeowner or board member, I see four summary boxes on the Dashboard: (1) Current Balance, (2) Violations, (3) Work Orders, and (4) Architecture Requests. For now, only the Violations box shows live data (count) and is clickable; the other three boxes display placeholder text only and are not linked to any page or action.

**Why this priority**: The four-box layout is part of the requested experience; the placeholders reserve space for future features.

**Independent Test**: Can be tested by confirming all four boxes are present with correct labels, the Violations box is the only one with a link, and the other three show placeholder text only.

**Acceptance Scenarios**:

1. **Given** I am on the Dashboard, **When** I view the page, **Then** I see exactly four summary boxes in a 2×2 grid: top row left-to-right Current Balance, Violations; bottom row left-to-right Work Orders, Architecture Requests.
2. **Given** I am on the Dashboard, **When** I view the Current Balance, Work Orders, and Architecture Requests boxes, **Then** each shows placeholder text only and is not clickable/linked.
3. **Given** I am on the Dashboard, **When** I view the Violations box, **Then** it shows the violation count and the count is clickable (other boxes are not).

---

### Edge Cases

- What happens when the user has no open violations? The violations box shows count 0; clicking it still opens My Violations page with an empty list or "no violations" message. Closed violations are out of scope for this feature (future).
- What happens while the violation count is loading? The Violations box shows a loading indicator (e.g. spinner or skeleton) until the count is available; the box is not clickable until the count is loaded.
- What happens when the violation count cannot be loaded (e.g. temporary failure)? User sees a clear, non-technical message and can retry or continue using the rest of the dashboard.
- How does the system handle a user who is both homeowner and board member? "My Violations" means only violations on the user's own property or properties (homeowner view). Board members see the same view: violations across all properties they own, not a separate list of violations they manage or review.
- What happens when the user clicks the violations box in a context where the number is not a link (e.g. if only the number is clickable)? Only the count number acts as the link to My Violations; the rest of the box may be non-clickable or decorative.
- Data isolation: The system must never show one user the violation count or violation records of another user; both the Dashboard count and My Violations list are scoped exclusively to the current user (see FR-010).

## Assumptions

- Login and authentication already exist or are in scope elsewhere; this spec focuses on post-login dashboard and the violations summary flow.
- "Violations" means HOA violations (e.g. property or conduct violations) that can be associated to a user or their property. For this feature, only open (unresolved) violations are counted and listed; closed violations are deferred to a future feature.
- "Associated to me" and "My Violations" mean violations across all properties the logged-in user owns (homeowner view). The count and list aggregate open violations across all of the user's properties. Board members see the same scope—no separate board work queue on this page.
- The My Violations page may already exist or will be built as part of this feature; it must show only violations for the current user (per definition above).
- The four boxes are displayed in a grid: two on top, two on bottom; left to right in the order Current Balance, Violations, Work Orders, Architecture Requests.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: After successful login, the system MUST show the Dashboard as the default landing page for homeowners and board members.
- **FR-002**: The Dashboard MUST display exactly four summary boxes in a grid: two on top, two on bottom; left to right in the order Current Balance, Violations, Work Orders, Architecture Requests.
- **FR-003**: The Violations box MUST display the count of open (unresolved) violations across all properties the logged-in user owns. Closed violations are excluded from the count; viewing closed violations is deferred to a future feature.
- **FR-004**: The violation count in that box MUST be clickable; selecting it MUST navigate the user to the "My Violations" page.
- **FR-005**: The "My Violations" page MUST list only open violations across all properties the current user owns (homeowner view). Board members see the same scope. Viewing closed violations is out of scope (future feature).
- **FR-006**: When the user has zero open violations, the violations box MUST show zero (or an equivalent "no violations" indication), and clicking it MUST still open the My Violations page.
- **FR-007**: While the violation count is loading, the Violations box MUST show a loading indicator (e.g. spinner or skeleton); the count link MUST NOT be clickable until the count is available.
- **FR-008**: The system MUST show a clear, user-friendly message if the violation count cannot be loaded (e.g. "Failed to load violation count"), without blocking access to the rest of the dashboard.
- **FR-009**: The Current Balance, Work Orders, and Architecture Requests boxes MUST display placeholder text only and MUST NOT link to any page or action.
- **FR-010**: The violation count and the My Violations list MUST be strictly scoped to the current user; no data belonging to another user (count or violation records) MUST be visible or accessible through the Dashboard or My Violations page.

### Key Entities

- **User (homeowner / board member)**: The logged-in actor; may hold one or both roles. "My Violations" is always violations across all properties the user owns (homeowner view).
- **Violation**: A record representing an HOA violation; must be associable to a user or property and have an open/closed (or equivalent) state. The dashboard count and My Violations list use only open violations, aggregated across all properties the user owns; closed violations are deferred to a future feature.
- **Dashboard**: Post-login view consisting of four summary boxes in a 2×2 grid (top row: Current Balance, Violations; bottom row: Work Orders, Architecture Requests). Only Violations is functional in this feature; the others show placeholder text.
- **My Violations page**: A dedicated page listing only open violations across all properties the current user owns (homeowner view), for both homeowners and board members. Closed violations are out of scope (future feature).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After login, users reach the Dashboard and see four summary boxes without needing to navigate elsewhere.
- **SC-002**: Users can go from Dashboard to My Violations in one click (on the violation count) and see the correct list.
- **SC-003**: The open violation count on the Dashboard matches the number of items shown on the My Violations page for that user (aggregated across all properties the user owns).
- **SC-004**: When the violation count is unavailable, users can still use the rest of the Dashboard and understand that the count could not be loaded.
