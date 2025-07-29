# HOA Management Company

A .NET 9.0 Blazor application for managing HOA violations and violation types with PostgreSQL database integration.

## Features

- **Violation Management**: Create, read, update, and delete HOA violations
- **Violation Types**: Manage different types of violations with covenant text
- **Database Integration**: Full PostgreSQL integration with Entity Framework Core
- **Integration Tests**: Comprehensive test suite with real database connectivity

## Technology Stack

- **.NET 9.0** - Latest .NET framework
- **Blazor Server** - Web UI framework
- **Entity Framework Core** - ORM for database operations
- **PostgreSQL** - Primary database
- **xUnit** - Testing framework
- **GitHub Actions** - CI/CD pipeline

## Quick Start

### Prerequisites

- .NET 9.0 SDK
- PostgreSQL 15+
- Docker (optional, for local development)

### Local Development

1. **Clone the repository**:
   ```bash
   git clone <repository-url>
   cd HOAManagementCompany
   ```

2. **Setup database**:
   ```bash
   # Using Docker
   docker run --name postgres-dev -e POSTGRES_PASSWORD=HXCKFJ3498fajjAJR94 -e POSTGRES_USER=sequestria1 -e POSTGRES_DB=sequestria -p 5432:5432 -d postgres:15
   
   # Or using the provided script
   ./scripts/setup-test-db.sh
   ```

3. **Run migrations**:
   ```bash
   cd HOAManagementCompany
   dotnet ef database update
   ```

4. **Start the application**:
   ```bash
   dotnet run
   ```

5. **Run tests**:
   ```bash
   dotnet test HOAManagementCompany.Tests
   ```

## Testing

### Integration Tests

The project includes comprehensive integration tests that:

- ✅ **Real Database Connectivity**: Tests connect to actual PostgreSQL database
- ✅ **CRUD Operations**: Complete Create, Read, Update, Delete functionality
- ✅ **Relationship Testing**: Foreign key constraints and entity relationships
- ✅ **Data Validation**: Both application-level and database-level validation
- ✅ **Transaction Management**: Rollback scenarios and error handling
- ✅ **Concurrent Access**: Multiple operations running simultaneously
- ✅ **Complex Queries**: Advanced filtering, ordering, and joining
- ✅ **Test Isolation**: Proper cleanup and resource management with namespaced data

### Running Tests

```bash
# Run all tests
dotnet test HOAManagementCompany.Tests

# Run with detailed output
dotnet test HOAManagementCompany.Tests --verbosity normal

# Run specific test class
dotnet test HOAManagementCompany.Tests --filter "FullyQualifiedName~ViolationIntegrationTests"
```

### Test Data Namespacing

All tests use namespaced data to prevent interference:
- Each test creates data with a unique prefix based on the test method name
- Example: `CreateViolation_ShouldSucceed` creates violation types named `CreateViolation_ShouldSucceed_GRASS_VIOLATION`
- This ensures tests can run in parallel without conflicts

## Continuous Integration

### GitHub Actions Workflow

The `.github/workflows/test.yml` workflow automatically:

1. **Sets up PostgreSQL 15** as a service container
2. **Runs database migrations** to create the test schema
3. **Executes all integration tests** against the test database
4. **Uploads test results** as artifacts

#### Workflow Triggers

- Push to `main` or `develop` branches
- Pull requests to `main` or `develop` branches

#### Environment Configuration

- **Database**: PostgreSQL 15 service container
- **Connection String**: `Host=localhost;Port=5432;Database=test_hoa;Username=postgres;Password=postgres`
- **Environment**: `ASPNETCORE_ENVIRONMENT=Development`

### Local CI Testing

To test the CI workflow locally:

```bash
# Setup test database
./scripts/setup-test-db.sh

# Run tests with CI environment
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=test_hoa;Username=postgres;Password=postgres"
dotnet test HOAManagementCompany.Tests
```

## Database Schema

### ViolationTypes Table
- `Id` (Guid, Primary Key)
- `Name` (string, Required)
- `CovenantText` (string, Required)

### Violations Table
- `Id` (Guid, Primary Key)
- `Description` (string, Required)
- `Status` (ViolationStatus enum: Open, Closed)
- `OccurrenceDate` (DateTime, Required)
- `ViolationTypeId` (Guid, Foreign Key to ViolationTypes)

## Project Structure

```
HOAManagementCompany/
├── Components/           # Blazor components
├── EntityFramework/      # Database context
├── Migrations/          # Entity Framework migrations
├── Models/              # Entity models
├── Services/            # Business logic services
└── wwwroot/            # Static assets

HOAManagementCompany.Tests/
├── TestBase.cs                    # Base test class
├── ViolationIntegrationTests.cs   # Violation entity tests
├── ViolationTypeIntegrationTests.cs # ViolationType entity tests
├── ComplexIntegrationTests.cs     # Complex workflow tests
└── README.md                      # Test documentation
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add/update tests
5. Ensure all tests pass
6. Submit a pull request

## License

[Add your license information here] 