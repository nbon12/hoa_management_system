<!--
Sync Impact Report
==================
Version change: (template/placeholder) → 1.0
Modified principles: N/A (initial ratification; replaced template placeholders)
Added sections: Project Purpose, Technology Stack, Backend Principles, Frontend Principles,
  Security & Authentication, Quality & Code Standards, Governance & Amendments,
  Spec Kit Testing Constitution (Purpose, Core Definitions, Guiding Principles,
  Repository/Business-Process/End-to-End Test Constitutions, Cross-Cutting Rules,
  Recommended Folder Layout, Strategic Outcome)
Removed sections: Template placeholders [PROJECT_NAME], [PRINCIPLE_*], [SECTION_*], [GOVERNANCE_RULES], etc.
Templates requiring updates:
  .specify/templates/plan-template.md ✅ (Constitution Check references constitution file generically)
  .specify/templates/spec-template.md ✅ (no constitution-specific placeholders)
  .specify/templates/tasks-template.md ✅ (task types align with testing discipline and quality standards)
Follow-up TODOs: None
-->

# HOA Portal Spec Kit Constitution

**Version**: 1.0 | **Ratified**: 2026-03-14 | **Last Amended**: 2026-03-14  
**Authors**: Project Lead

## 1. Project Purpose

This project provides a web portal for Homeowners, Board Members, and HOA Managers to interact with HOA data securely and efficiently. All features must align with the following principles:

- Security, privacy, and proper authentication for all user types.
- Consistency and predictability of API behavior.
- Maintainability and scalability of code.
- Accessibility and responsiveness across devices.

## 2. Technology Stack

All implementations MUST conform to the following technologies:

- **Backend**: .NET REST API (HTTP verbs: GET, POST, PUT, DELETE, etc.)
- **Database**: PostgreSQL
- **Frontend**: Angular
- **Authentication/Authorization**: Okta (must handle all user types and roles)
- **CI/CD**: GitHub Actions
- **Testing**: Unit tests, integration tests, and UI responsiveness tests

## 3. Backend Principles

### Statelessness

- The API MUST be as stateless as possible.
- Any state changes MUST persist in the database.
- Function calls MUST minimize side effects.

### Functional Paradigm

- Prefer functional programming patterns.
- Use factories to create data and setup relationships.
- Factories MUST NOT modify objects they didn't create.
- Services are allowed to modify object state.

### Error Handling

- A global exception handler MUST manage all errors consistently.
- API responses MUST provide meaningful error messages with proper HTTP status codes.

### Pagination

- All endpoints that return collections MUST support pagination via `limit` and `offset` query parameters.

## 4. Frontend Principles

### Responsiveness

UI MUST function and render correctly on:

- iPhone widths
- Tablet widths
- Desktop widths

### Consistency

- UI components MUST follow a consistent design system and Angular conventions.
- All user interactions MUST be tested and validated against the backend API contracts.

## 5. Security & Authentication

- All endpoints and UI actions MUST enforce Okta authentication.
- User roles (Homeowner, Board Member, HOA Manager) MUST be strictly separated.
- Sensitive data MUST be encrypted at rest and in transit.
- API MUST NOT return more data than necessary for the authenticated role.

## 6. Quality & Code Standards

### Backend

- Adhere to .NET naming conventions and code style.
- All functions MUST have unit tests.
- Use integration tests to validate database interactions.

### Frontend

- Use Angular best practices and linting.
- Component unit tests are required.

### CI/CD

- All tests MUST pass before code merges.
- GitHub Actions workflows MUST enforce linting, testing, and deployment rules.

## 7. Governance & Amendments

- Any change to this constitution MUST be approved via a formal review process.
- Each amendment MUST document the rationale and author.
- Constitution versioning MUST be maintained, and old versions archived for reference.

---

# Spec Kit Testing Constitution

(.NET + PostgreSQL, Transaction-per-Test Isolation)

## Purpose

This constitution defines the rules, principles, and best practices for writing automated tests that are:

- **Deterministic** – repeatable and order-independent
- **Isolated** – test data does not interfere with other tests
- **Realistic** – uses PostgreSQL with production semantics
- **Scalable** – maintainable as the system and team grow

It applies to unit, repository, business-process, and end-to-end tests.

## 1. Core Definitions

### 1.1 Business Process

A business process is:

A named, intention-revealing operation that coordinates multiple domain rules, state transitions, and side effects to achieve a business outcome.

**Characteristics:**

- Lives in Application Service, Domain Service, or Use Case
- May span multiple repositories
- May emit events, enqueue jobs, or send notifications
- Often has transactional boundaries

**Examples:** OnboardHomeOwner, ApproveBoardMember, GenerateMonthlyAssessment

**Not a business process:** repository CRUD, query handlers, mapping, or pure domain methods

### 1.2 Repository

A repository is a persistence abstraction responsible for:

- CRUD operations
- Queries and projections
- Transaction boundaries for persisted data

Repositories are not business processes and are tested separately.

## 2. Guiding Principles

### 2.1 Universal Isolation

All tests MUST execute inside a PostgreSQL transaction that is rolled back after the test.

This ensures:

- Full isolation per test
- Parallel execution safety
- No cross-test contamination

**Example Pattern (C# + EF Core):**

```csharp
await using var transaction = await dbContext.Database.BeginTransactionAsync();
// create test data
// execute method under test
// assertions
await transaction.RollbackAsync();
```

### 2.2 PostgreSQL Usage

All tests that interact with persistence MUST use PostgreSQL.

- Avoid in-memory substitutes (SQLite, EF Core in-memory, mocks).
- Ensures tests reflect production constraints, types, and transaction semantics.

### 2.3 Factories

**Purpose:** Factories exist to declare valid data state for tests. They MUST NOT execute business logic.

**Allowed:**

- Explicit field values
- Required attributes for tables or entities
- Default foreign key references

**Example:**

```csharp
var adminUser = UserFactory.Create(role: "Admin", discountRate: 0);
```

**Prohibited:**

- Conditional business rules inside factories
- Applying side effects based on domain logic

```csharp
// ❌ Not allowed
if (role == "Admin") ApplyAdminDiscount();
```

**Rationale:** Mixing business logic into factories creates hidden side effects; tests may pass/fail due to setup code rather than the system under test; makes factories brittle and tightly coupled to business rules. Keeps factories reusable, predictable, and declarative.

## 3. Repository Test Constitution

- Repository tests MUST be isolated in their own transaction.
- All required data MUST be created directly in the test or via factories.
- Repository tests verify structural correctness only: CRUD operations, queries returning expected rows, constraints enforcement.
- MUST NOT invoke business processes or validate domain rules.

**Lifecycle Example:**

```text
Environment Setup (once per test suite)
  ├─ PostgreSQL test database created
  ├─ EF Core migrations applied
  └─ Reference data seeded (roles, enums)

Per Test
  ├─ Begin transaction
  ├─ Insert necessary rows
  ├─ Execute repository method
  ├─ Assert structural correctness
  └─ Rollback transaction
```

## 4. Business-Process Test Constitution

- Business-process tests MUST be isolated in their own transaction.
- Each test creates minimal valid domain state, even if normally produced by another business process.
- Tests MUST NOT invoke other business processes, except for explicit end-to-end tests.
- Assertions MUST focus on business outcomes, not repository implementation.

**Lifecycle Example:**

```text
Environment Setup (once per suite)
  ├─ PostgreSQL test database created
  ├─ EF Core migrations applied
  ├─ Seed roles, enums, and static reference data

Per Test
  ├─ Begin transaction
  ├─ Create valid domain state (factories)
  ├─ Execute business process
  ├─ Assert business outcomes
  └─ Rollback transaction
```

## 5. End-to-End Test Constitution

- May invoke multiple business processes.
- Validate full workflows, reflecting production behavior.
- Should be few in number.
- Isolation still required via per-test transaction.
- Assertions focus on orchestration correctness, not structural details.

## 6. Cross-Cutting Rules

- **Order Independence** – tests pass in any order
- **Repeatability** – tests pass on repeated runs
- **Failure Isolation** – tests fail for one reason only
- **Data Ownership** – tests own all data they require
- **Production Faithfulness** – PostgreSQL semantics match production

## 7. Recommended Folder Layout

```text
spec_kit/
  testing/
    constitution.md             # This main document
    repository.constitution.md  # Optional modular supplement
    business_process.constitution.md
    end_to_end.constitution.md
  factories/
    UserFactory.cs
    AssessmentFactory.cs
  fixtures/
    TestDatabaseFixture.cs      # handles per-test transaction rollback
  migrations/
    001_init.sql
    002_roles.sql
```

## 8. Strategic Outcome

- **Repository tests:** validate persistence independently of domain rules
- **Business-process tests:** validate domain behavior independently of other processes
- **End-to-end tests:** validate orchestration and workflow correctness
- **All tests:** fully isolated, deterministic, production-faithful, and safe for parallel execution
