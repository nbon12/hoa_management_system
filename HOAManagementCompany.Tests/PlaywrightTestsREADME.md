# Playwright End-to-End Tests

This directory contains Playwright end-to-end tests for the HOA Management Company application, specifically testing the ViolationTypes CRUD operations from an admin user perspective.

## Overview

The Playwright tests simulate real user interactions with the Blazor application, testing the complete workflow for managing violation types. These tests run against the actual running application and verify both UI behavior and database state.

## Test Coverage

### ViolationTypes CRUD Operations

#### View Tests
- **Admin_CanViewViolationTypesList**: Verifies admin can see the violation types list with proper table structure
- **Admin_CanViewEmptyViolationTypesList**: Tests the empty state when no violation types exist
- **Admin_CanViewTableStructure**: Verifies proper table headers and action buttons

#### Create Tests
- **Admin_CanCreateNewViolationType**: Tests the complete creation workflow
  - Navigate to create form
  - Fill out required fields
  - Submit form
  - Verify redirect and database persistence

#### Read Tests
- **Admin_CanViewViolationTypesList**: Verifies existing violation types are displayed
- **Admin_CanPerformCompleteCRUDWorkflow**: Includes read verification in the workflow

#### Update Tests
- **Admin_CanEditExistingViolationType**: Tests the complete edit workflow
  - Navigate to edit form
  - Verify pre-filled data
  - Update fields
  - Submit form
  - Verify changes in UI and database

#### Delete Tests
- **Admin_CanDeleteViolationType**: Tests successful deletion with confirmation
- **Admin_CanCancelViolationTypeDeletion**: Tests cancellation of deletion
- **Admin_CanHandleDeleteWithRelatedViolations**: Tests foreign key constraint handling

#### Navigation Tests
- **Admin_CanCancelFormSubmission**: Tests cancel button functionality
- **Admin_CanHandleInvalidEditId**: Tests error handling for invalid IDs

#### Workflow Tests
- **Admin_CanPerformCompleteCRUDWorkflow**: End-to-end test of all CRUD operations
- **Admin_CanHandleFormValidationErrors**: Tests form validation

## Prerequisites

### Required Software
- .NET 9.0 SDK
- Docker (for PostgreSQL)
- Playwright browsers (installed automatically)

### Running Services
- PostgreSQL database (via Docker)
- Blazor application (running on https://localhost:7255)

## Setup Instructions

### 1. Install Playwright
```bash
# Install Playwright CLI globally
dotnet tool install --global Microsoft.Playwright.CLI

# Install browsers
playwright install
```

### 2. Start Required Services
```bash
# Start PostgreSQL database
cd ..
docker-compose up -d postgres-db

# Start the Blazor application
cd HOAManagementCompany
dotnet run
```

### 3. Run Tests
```bash
# Using the provided script (recommended)
cd HOAManagementCompany.Tests
./run-playwright-tests.sh

# Or manually
dotnet test --filter "FullyQualifiedName~ViolationTypesPlaywrightTests"
```

## Test Architecture

### TestBase Integration
The Playwright tests extend `TestBase` to leverage:
- Database connectivity and cleanup
- Unique test data namespacing
- ViolationService access for database verification

### Unique Test Data
Each test uses unique namespaces to prevent conflicts:
```csharp
private string _testNamespace = await GenerateUniqueTestNamespaceAsync();
private string _adminUserName = $"Admin_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{new Random().Next(1000, 9999)}";
```

### Browser Configuration
```csharp
_browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Headless = false, // Set to true for CI/CD
    SlowMo = 100 // Slow down for debugging
});
```

## Test Data Management

### Creation
Tests create unique violation types using the test namespace:
```csharp
var violationType = await CreateTestViolationTypeAsync(_testNamespace, 
    $"{_adminUserName}_Test_Violation", 
    "Test covenant text");
```

### Cleanup
Automatic cleanup after each test:
```csharp
public async Task DisposeAsync()
{
    await CleanupTestDataAsync(_testNamespace);
    // Dispose Playwright resources
}
```

## Assertion Strategy

### UI Assertions
- Element presence and content verification
- Navigation confirmation
- Form state validation

### Database Assertions
- Direct database verification using ViolationService
- Data persistence confirmation
- Foreign key constraint testing

### Dialog Handling
- JavaScript confirm/alert dialog testing
- Error message verification

## Running Specific Tests

### Run All ViolationTypes Tests
```bash
dotnet test --filter "FullyQualifiedName~ViolationTypesPlaywrightTests"
```

### Run Specific Test
```bash
dotnet test --filter "FullyQualifiedName~Admin_CanCreateNewViolationType"
```

### Run with Verbose Output
```bash
dotnet test --filter "FullyQualifiedName~ViolationTypesPlaywrightTests" --verbosity normal
```

## Debugging

### Visual Debugging
Tests run with `Headless = false` by default, allowing you to see the browser interactions.

### Slow Motion
Tests include `SlowMo = 100` to slow down interactions for debugging.

### Screenshots
Playwright automatically takes screenshots on test failure.

### Console Logs
Browser console logs are captured and displayed in test output.

## CI/CD Integration

### Headless Mode
For CI/CD, set `Headless = true` in the browser launch options.

### Docker Support
Tests work with the existing Docker PostgreSQL setup.

### GitHub Actions
The tests can be integrated into the existing GitHub Actions workflow.

## Troubleshooting

### Common Issues

1. **Application Not Running**
   ```
   ❌ Application is not running on https://localhost:7255
   ```
   **Solution**: Start the Blazor application with `dotnet run`

2. **PostgreSQL Not Available**
   ```
   ❌ PostgreSQL container is not running
   ```
   **Solution**: Start PostgreSQL with `docker-compose up -d postgres-db`

3. **Browser Not Found**
   ```
   Browser not found
   ```
   **Solution**: Install browsers with `playwright install`

4. **SSL Certificate Issues**
   ```
   SSL certificate errors
   ```
   **Solution**: The application uses HTTPS with a development certificate

### Test Isolation
- Each test uses unique data namespaces
- Automatic cleanup after each test
- No shared state between tests

### Performance
- Tests run sequentially to avoid conflicts
- Browser instances are reused within test classes
- Database cleanup is optimized

## Future Enhancements

### Authentication Testing
When authentication is implemented, tests can be extended to:
- Login/logout workflows
- Role-based access control
- Session management

### Additional Test Scenarios
- Bulk operations
- Search and filtering
- Export functionality
- Mobile responsiveness

### Visual Regression Testing
- Screenshot comparison
- Layout verification
- Cross-browser testing

## Contributing

When adding new Playwright tests:

1. Follow the existing naming convention: `Admin_Can[Action]`
2. Use unique test data namespaces
3. Include both UI and database assertions
4. Handle dialogs and error states
5. Add appropriate documentation

## Related Files

- `ViolationTypesPlaywrightTests.cs` - Main test file
- `run-playwright-tests.sh` - Test runner script
- `TestBase.cs` - Base class with database utilities
- `ViolationService.cs` - Service for database operations 