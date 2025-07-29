# Integration Tests

This directory contains integration tests for the HOA Management Company application.

## Test Structure

- **TestBase.cs**: Base class providing database setup, cleanup, and helper methods
- **ViolationIntegrationTests.cs**: Tests for Violation entity operations
- **ViolationTypeIntegrationTests.cs**: Tests for ViolationType entity operations  
- **ComplexIntegrationTests.cs**: Complex workflow and performance tests

## Local Development

### Prerequisites

- .NET 9.0 SDK
- PostgreSQL 15+ running locally
- Entity Framework CLI tools

### Running Tests Locally

1. **Setup test database** (one-time setup):
   ```bash
   ./scripts/setup-test-db.sh
   ```

2. **Run tests**:
   ```bash
   # Using the test database
   export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=test_hoa;Username=postgres;Password=postgres"
   dotnet test HOAManagementCompany.Tests
   
   # Or using your local development database
   dotnet test HOAManagementCompany.Tests
   ```

### Test Data Namespacing

All tests use namespaced data to prevent interference:
- Each test creates data with a unique prefix based on the test method name
- Example: `CreateViolation_ShouldSucceed` creates violation types named `CreateViolation_ShouldSucceed_GRASS_VIOLATION`
- This ensures tests can run in parallel without conflicts

## GitHub Actions

The `.github/workflows/test.yml` workflow automatically:

1. **Sets up PostgreSQL 15** as a service container
2. **Runs database migrations** to create the test schema
3. **Executes all integration tests** against the test database
4. **Uploads test results** as artifacts

### Workflow Triggers

- Push to `main` or `develop` branches
- Pull requests to `main` or `develop` branches

### Environment Variables

The workflow uses these environment variables:
- `ConnectionStrings__DefaultConnection`: Points to the test PostgreSQL instance
- `ASPNETCORE_ENVIRONMENT`: Set to `Development`

## Test Database Configuration

### Local Development
- **Database**: `sequestria` (from appsettings.json)
- **User**: `sequestria1`
- **Password**: `HXCKFJ3498fajjAJR94`

### CI/CD (GitHub Actions)
- **Database**: `test_hoa`
- **User**: `postgres`
- **Password**: `postgres`

## Troubleshooting

### Common Issues

1. **PostgreSQL not running**:
   ```bash
   # Start with Docker
   docker run --name postgres-test -e POSTGRES_PASSWORD=postgres -e POSTGRES_USER=postgres -e POSTGRES_DB=test_hoa -p 5432:5432 -d postgres:15
   ```

2. **Migration errors**:
   ```bash
   cd HOAManagementCompany
   dotnet ef database update
   ```

3. **Connection string issues**:
   - Ensure the connection string environment variable is set correctly
   - Check that PostgreSQL is accessible on localhost:5432

### Test Cleanup

Tests automatically clean up their data after execution. If you encounter issues:

1. **Manual cleanup**:
   ```sql
   -- Connect to your database and run:
   DELETE FROM "Violations" WHERE "Description" LIKE '%_%';
   DELETE FROM "ViolationTypes" WHERE "Name" LIKE '%_%' AND "Id" NOT IN (SELECT DISTINCT "ViolationTypeId" FROM "Violations");
   ```

2. **Reset database**:
   ```bash
   cd HOAManagementCompany
   dotnet ef database drop --force
   dotnet ef database update
   ```
