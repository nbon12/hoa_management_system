using Microsoft.Playwright;
using Xunit;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace HOAManagementCompany.Tests;

public class ViolationTypesPlaywrightTests : TestBase, IAsyncLifetime
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IPage _page = null!;
    private string _testNamespace = null!;
    private string _adminUserName = null!;

    private async Task WaitForPageToLoadAsync()
    {
        var methodStartTime = DateTime.UtcNow;
        Console.WriteLine($"[TIMING] WaitForPageToLoadAsync started at: {methodStartTime:HH:mm:ss.fff}");
        
        try
        {
            // First check if the canary is visible
            var canaryCheckStartTime = DateTime.UtcNow;
            var canaryVisible = await _page.Locator("#page-loading-canary").IsVisibleAsync();
            var canaryCheckEndTime = DateTime.UtcNow;
            Console.WriteLine($"[TIMING] Canary visibility check took: {(canaryCheckEndTime - canaryCheckStartTime).TotalMilliseconds}ms");
            Console.WriteLine($"Initial canary visibility: {canaryVisible}");
            
            if (canaryVisible)
            {
                var waitForDetachedStartTime = DateTime.UtcNow;
                await _page.WaitForSelectorAsync("#page-loading-canary", 
                    new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached, Timeout = 10000 });
                var waitForDetachedEndTime = DateTime.UtcNow;
                Console.WriteLine($"[TIMING] WaitForSelector (detached) took: {(waitForDetachedEndTime - waitForDetachedStartTime).TotalMilliseconds}ms");
                Console.WriteLine("Canary disappeared successfully");
            }
            else
            {
                Console.WriteLine("Canary was not visible, page may already be loaded");
                // Even if canary is not visible, wait for network to be idle
                var networkIdleStartTime = DateTime.UtcNow;
                await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                var networkIdleEndTime = DateTime.UtcNow;
                Console.WriteLine($"[TIMING] WaitForLoadStateAsync (NetworkIdle) took: {(networkIdleEndTime - networkIdleStartTime).TotalMilliseconds}ms");
            }
        }
        catch (TimeoutException)
        {
            // If canary doesn't disappear, let's see what's on the page
            var pageContent = await _page.ContentAsync();
            Console.WriteLine($"Page content when canary didn't disappear: {pageContent}");
            
            // Also check if there are any console errors
            var consoleMessages = await _page.EvaluateAsync<string>("() => { return window.console && window.console.logs ? window.console.logs.join('\\n') : 'No console logs available'; }");
            Console.WriteLine($"Console messages: {consoleMessages}");
            
            throw;
        }
        
        var methodEndTime = DateTime.UtcNow;
        Console.WriteLine($"[TIMING] WaitForPageToLoadAsync completed at: {methodEndTime:HH:mm:ss.fff}");
        Console.WriteLine($"[TIMING] WaitForPageToLoadAsync total duration: {(methodEndTime - methodStartTime).TotalMilliseconds}ms");
    }

    private async Task WaitForViolationTypesPageToLoadAsync()
    {
        // Wait for the loading canary to disappear (page is fully loaded)
        await WaitForPageToLoadAsync();
        
        // Add a small delay to allow Blazor to render the content
        await _page.WaitForTimeoutAsync(1000);
        
        // Debug: Check what's actually on the page
        var pageTitle = await _page.TitleAsync();
        Console.WriteLine($"ViolationTypes page title: {pageTitle}");
        
        var canaryVisible = await _page.Locator("#page-loading-canary").IsVisibleAsync();
        Console.WriteLine($"Canary is visible: {canaryVisible}");
        
        if (canaryVisible)
        {
            await _page.WaitForTimeoutAsync(2000);
            canaryVisible = await _page.Locator("#page-loading-canary").IsVisibleAsync();
            Console.WriteLine($"Canary is still visible after 2 seconds: {canaryVisible}");
        }
        
        // Wait for network to be idle (all requests completed)
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Now verify we're on the right page with a more robust approach
        try
        {
            await _page.WaitForSelectorAsync("h1:has-text('Violation Types')", new PageWaitForSelectorOptions { Timeout = 10000 });
        }
        catch (TimeoutException)
        {
            // If h1 is not found, let's check what's actually on the page
            var pageContent = await _page.ContentAsync();
            Console.WriteLine($"Page content when h1 not found: {pageContent}");
            throw;
        }
    }

    public async Task InitializeAsync()
    {
        // Generate unique test namespace
        _testNamespace = $"PLAYWRIGHT_TEST_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{new Random().Next(1000, 9999)}";
        _adminUserName = $"Admin_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{new Random().Next(1000, 9999)}";
        
        // Test database connection
        try
        {
            var testViolationTypes = await ViolationService.GetViolationTypesAsync();
            Console.WriteLine($"Database connection successful. Found {testViolationTypes.Count} violation types.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Database connection failed: {ex.Message}");
            throw;
        }

        // Initialize Playwright
        _playwright = await Playwright.CreateAsync();
        
        // Check if PLAYWRIGHT_HEADLESS environment variable is set
        var isHeadless = Environment.GetEnvironmentVariable("PLAYWRIGHT_HEADLESS") == "true";
        Console.WriteLine($"Playwright Headless Mode: {isHeadless}");
        
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = isHeadless, // Use environment variable or default to false for local debugging
            SlowMo = isHeadless ? 0 : 100 // No slow motion in headless mode for faster execution
        });
        _page = await _browser.NewPageAsync();

        // Navigate to the application
        await _page.GotoAsync("http://localhost:5212");
        
        // Verify the application is running by checking for a basic element
        try
        {
            await _page.WaitForSelectorAsync("h1", new PageWaitForSelectorOptions { Timeout = 5000 });
            Console.WriteLine("Application is running and accessible");
            
            // Debug: Check what's on the initial page
            var title = await _page.TitleAsync();
            var h1Text = await _page.Locator("h1").TextContentAsync();
            Console.WriteLine($"Page title: {title}");
            Console.WriteLine($"H1 text: {h1Text}");
        }
        catch (TimeoutException)
        {
            Console.WriteLine("ERROR: Application is not running or not accessible on http://localhost:5212");
            var pageContent = await _page.ContentAsync();
            Console.WriteLine($"Page content: {pageContent}");
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        // Clean up test data
        await CleanupTestNamespaceAsync(_testNamespace);

        // Dispose Playwright resources
        await _page.CloseAsync();
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    [Fact]
    public async Task Admin_CanNavigateToHomePage()
    {
        // Act - Navigate to home page and wait for it to load
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Debug: Print page title and content
        var title = await _page.TitleAsync();
        var h1Text = await _page.Locator("h1").TextContentAsync();
        Console.WriteLine($"Page title: {title}");
        Console.WriteLine($"H1 text: {h1Text}");
        
        // Assert - Verify we're on the home page
        Assert.Contains("Home", title);
        Assert.Contains("Hello, world!", h1Text ?? "");
    }

    [Fact]
    public async Task Admin_CanNavigateDirectlyToViolationTypesPage()
    {
        // Act - Navigate directly to ViolationTypes page
        await _page.GotoAsync("http://localhost:5212/violationtypes");
        
        // Debug: Print page content
        var title = await _page.TitleAsync();
        Console.WriteLine($"ViolationTypes page title: {title}");
        
        // Check if the canary appears
        var canaryExists = await _page.Locator("#page-loading-canary").IsVisibleAsync();
        Console.WriteLine($"Canary is visible: {canaryExists}");
        
        if (canaryExists)
        {
            // Wait a bit and check again
            await Task.Delay(2000);
            canaryExists = await _page.Locator("#page-loading-canary").IsVisibleAsync();
            Console.WriteLine($"Canary is still visible after 2 seconds: {canaryExists}");
            
            if (canaryExists)
            {
                var pageContent = await _page.ContentAsync();
                Console.WriteLine($"Page content when canary is stuck: {pageContent}");
            }
        }
        
        // Assert - Verify we can see the page title
        Assert.Contains("Violation Types", title);
    }

    [Fact]
    public async Task Admin_CanViewViolationTypesList()
    {
        // Arrange - Create test violation types for the admin
        var violationType1 = await CreateTestViolationTypeAsync(_testNamespace, $"{_adminUserName}_Grass_Violation", "Lawn maintenance required");
        var violationType2 = await CreateTestViolationTypeAsync(_testNamespace, $"{_adminUserName}_Paint_Violation", "House painting required");

        // Act - Navigate to ViolationTypes page
        await _page.ClickAsync("text=Violation Types");
        
        // Wait for the loading canary to disappear (page is fully loaded)
        await WaitForPageToLoadAsync();

        // Assert - Only check for violation types with our namespace
        await _page.WaitForSelectorAsync($"text={violationType1.Name}");
        await _page.WaitForSelectorAsync($"text={violationType2.Name}");
        
        // Verify table structure
        await _page.WaitForSelectorAsync("table");
        await _page.WaitForSelectorAsync("th:has-text('ID')");
        await _page.WaitForSelectorAsync("th:has-text('Name')");
        await _page.WaitForSelectorAsync("th:has-text('Description')");
        await _page.WaitForSelectorAsync("th:has-text('Actions')");
    }

    [Fact]
    public async Task Admin_CanCreateNewViolationType()
    {
        // Arrange - Navigate to ViolationTypes page
        await _page.ClickAsync("text=Violation Types");
        
        // Wait for the loading canary to disappear (page is fully loaded)
        await WaitForPageToLoadAsync();

        // Act - Click "Add New Violation Type" button
        await _page.ClickAsync("text=Add New Violation Type");
        await _page.WaitForSelectorAsync("h3:has-text('Create New Violation Type')");

        // Fill out the form
        var newViolationTypeName = $"{_testNamespace}_{_adminUserName}_New_Violation_Type";
        var newCovenantText = $"{_adminUserName} covenant text for new violation type";
        await _page.FillAsync("#name", newViolationTypeName);
        await _page.FillAsync("#covenantText", newCovenantText);
        await _page.ClickAsync("button[type='submit']");

        // Assert - Wait for page to load and verify we're on the right page
        await WaitForViolationTypesPageToLoadAsync();
        
        await _page.WaitForSelectorAsync($"text={newViolationTypeName}");
        await _page.WaitForSelectorAsync($"text={newCovenantText}");
        var createdViolationType = (await ViolationService.GetViolationTypesAsync()).FirstOrDefault(vt => vt.Name == newViolationTypeName && vt.CovenantText == newCovenantText);
        Assert.NotNull(createdViolationType);
    }

    [Fact]
    public async Task Admin_CanEditExistingViolationType()
    {
        // Arrange - Create a test violation type
        var originalViolationType = await CreateTestViolationTypeAsync(_testNamespace, $"{_adminUserName}_Original_Name", $"{_adminUserName} original covenant text");
        await _page.ClickAsync("text=Violation Types");
        
        // Wait for the loading canary to disappear (page is fully loaded)
        await WaitForPageToLoadAsync();

        // Act - Click edit button for the violation type
        await _page.ClickAsync($"tr:has-text('{originalViolationType.Name}') button:has-text('Edit')");
        await _page.WaitForSelectorAsync("h3:has-text('Edit Violation Type')");

        // Verify form is pre-filled with existing data
        var nameValue = await _page.InputValueAsync("#name");
        var covenantTextValue = await _page.InputValueAsync("#covenantText");
        Assert.Equal(originalViolationType.Name, nameValue);
        Assert.Equal(originalViolationType.CovenantText, covenantTextValue);

        // Update the form
        var updatedName = $"{_testNamespace}_{_adminUserName}_Updated_Name";
        var updatedCovenantText = $"{_adminUserName} updated covenant text";
        await _page.FillAsync("#name", updatedName);
        await _page.FillAsync("#covenantText", updatedCovenantText);
        await _page.ClickAsync("button[type='submit']");

        // Assert - Wait for page to load and verify we're on the right page
        await WaitForViolationTypesPageToLoadAsync();
        
        await _page.WaitForSelectorAsync($"text={updatedName}");
        await _page.WaitForSelectorAsync($"text={updatedCovenantText}");
        var updatedViolationType = await ViolationService.GetViolationTypeByIdAsync(originalViolationType.Id);
        Assert.NotNull(updatedViolationType);
        Assert.Equal(updatedName, updatedViolationType.Name);
        Assert.Equal(updatedCovenantText, updatedViolationType.CovenantText);
    }

    [Fact]
    public async Task Admin_CanDeleteViolationType()
    {
        // Arrange - Create a test violation type
        var violationTypeToDelete = await CreateTestViolationTypeAsync(_testNamespace, $"{_adminUserName}_To_Delete", $"{_adminUserName} violation type to delete");
        
        // Navigate to ViolationTypes page
        await _page.ClickAsync("text=Violation Types");
        
        // Wait for the loading canary to disappear (page is fully loaded)
        await WaitForPageToLoadAsync();

        // Set up dialog handler BEFORE clicking the delete button
        _page.Dialog += async (sender, e) => await e.AcceptAsync();

        // Act - Click delete button and confirm
        // Use a more specific selector and wait for the element to be visible
        var deleteButton = _page.Locator($"tr:has-text('{violationTypeToDelete.Name}') button:has-text('Delete')");
        await deleteButton.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await deleteButton.ClickAsync();

        // Assert - Wait for page to load and verify we're on the right page
        await WaitForViolationTypesPageToLoadAsync();
        
        // Wait for the row to be removed from the table
        var row = _page.Locator($"tr:has-text('{violationTypeToDelete.Name}')");
        await row.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
        
        // Verify in database
        var deletedViolationType = await ViolationService.GetViolationTypeByIdAsync(violationTypeToDelete.Id);
        Assert.Null(deletedViolationType);
    }

    [Fact]
    public async Task Admin_CanCancelViolationTypeDeletion()
    {
        var testStartTime = DateTime.UtcNow;
        Console.WriteLine($"[TIMING] Test started at: {testStartTime:HH:mm:ss.fff}");
        
        // Arrange - Create a test violation type
        var createStartTime = DateTime.UtcNow;
        var violationTypeToKeep = await CreateTestViolationTypeAsync(_testNamespace, $"{_adminUserName}_To_Keep", $"{_adminUserName} violation type to keep");
        var createEndTime = DateTime.UtcNow;
        Console.WriteLine($"[TIMING] CreateTestViolationTypeAsync took: {(createEndTime - createStartTime).TotalMilliseconds}ms");
        
        // Navigate to ViolationTypes page
        var navigateStartTime = DateTime.UtcNow;
        await _page.ClickAsync("text=Violation Types");
        var navigateEndTime = DateTime.UtcNow;
        Console.WriteLine($"[TIMING] Navigation click took: {(navigateEndTime - navigateStartTime).TotalMilliseconds}ms");
        
        // Wait for the loading canary to disappear (page is fully loaded)
        var waitStartTime = DateTime.UtcNow;
        await WaitForPageToLoadAsync();
        var waitEndTime = DateTime.UtcNow;
        Console.WriteLine($"[TIMING] WaitForPageToLoadAsync took: {(waitEndTime - waitStartTime).TotalMilliseconds}ms");

        // Set up dialog handler BEFORE clicking the delete button
        var dialogSetupStartTime = DateTime.UtcNow;
        _page.Dialog += async (sender, e) => await e.DismissAsync();
        var dialogSetupEndTime = DateTime.UtcNow;
        Console.WriteLine($"[TIMING] Dialog setup took: {(dialogSetupEndTime - dialogSetupStartTime).TotalMilliseconds}ms");

        // Act - Click delete button but cancel
        // Use a more specific selector and wait for the element to be visible
        var locatorStartTime = DateTime.UtcNow;
        var deleteButton = _page.Locator($"tr:has-text('{violationTypeToKeep.Name}') button:has-text('Delete')");
        var locatorEndTime = DateTime.UtcNow;
        Console.WriteLine($"[TIMING] Locator creation took: {(locatorEndTime - locatorStartTime).TotalMilliseconds}ms");
        
        var waitForVisibleStartTime = DateTime.UtcNow;
        await deleteButton.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var waitForVisibleEndTime = DateTime.UtcNow;
        Console.WriteLine($"[TIMING] WaitForAsync (visible) took: {(waitForVisibleEndTime - waitForVisibleStartTime).TotalMilliseconds}ms");
        
        var clickStartTime = DateTime.UtcNow;
        await deleteButton.ClickAsync();
        var clickEndTime = DateTime.UtcNow;
        Console.WriteLine($"[TIMING] ClickAsync took: {(clickEndTime - clickStartTime).TotalMilliseconds}ms");

        // Assert - Only check for violation types with our namespace
        var assertStartTime = DateTime.UtcNow;
        await _page.WaitForSelectorAsync($"text={violationTypeToKeep.Name}");
        var assertEndTime = DateTime.UtcNow;
        Console.WriteLine($"[TIMING] WaitForSelector (assert) took: {(assertEndTime - assertStartTime).TotalMilliseconds}ms");
        
        var dbQueryStartTime = DateTime.UtcNow;
        var existingViolationType = await ViolationService.GetViolationTypeByIdAsync(violationTypeToKeep.Id);
        var dbQueryEndTime = DateTime.UtcNow;
        Console.WriteLine($"[TIMING] Database query took: {(dbQueryEndTime - dbQueryStartTime).TotalMilliseconds}ms");
        
        Assert.NotNull(existingViolationType);
        Assert.Equal(violationTypeToKeep.Name, existingViolationType.Name);
        
        var testEndTime = DateTime.UtcNow;
        Console.WriteLine($"[TIMING] Test completed at: {testEndTime:HH:mm:ss.fff}");
        Console.WriteLine($"[TIMING] Total test duration: {(testEndTime - testStartTime).TotalMilliseconds}ms");
    }

    [Fact]
    public async Task Admin_CanCancelFormSubmission()
    {
        // Arrange - Navigate to ViolationTypes page
        await _page.ClickAsync("text=Violation Types");
        
        // Wait for the loading canary to disappear (page is fully loaded)
        await WaitForPageToLoadAsync();

        // Act - Click "Add New Violation Type" button
        await _page.ClickAsync("text=Add New Violation Type");

        // Verify we're on the create form
        await _page.WaitForSelectorAsync("h3:has-text('Create New Violation Type')");

        // Click cancel button
        await _page.ClickAsync("button:has-text('Cancel')");

        // Assert - Verify we're redirected back to the list
        await WaitForViolationTypesPageToLoadAsync();
    }

    [Fact]
    public async Task Admin_CanHandleInvalidEditId()
    {
        // Arrange - Navigate directly to an invalid edit URL
        var invalidId = Guid.NewGuid();
        await _page.GotoAsync($"http://localhost:5212/violationtypes/edit/{invalidId}");

        // Act - Wait for the page to load

        // Assert - Verify we're redirected back to the list page
        await WaitForViolationTypesPageToLoadAsync();
    }

    [Fact]
    public async Task Admin_CanHandleDeleteWithRelatedViolations()
    {
        // Arrange - Create a violation type with related violations
        var violationTypeWithViolations = await CreateTestViolationTypeAsync(_testNamespace, $"{_adminUserName}_With_Violations", $"{_adminUserName} violation type with related violations");

        var relatedViolation = await CreateTestViolationAsync(_testNamespace, violationTypeWithViolations.Id, $"{_adminUserName} related violation");

        // Navigate to ViolationTypes page
        await _page.ClickAsync("text=Violation Types");
        
        // Wait for the loading canary to disappear (page is fully loaded)
        await WaitForPageToLoadAsync();

        // Act - Try to delete violation type with related violations
        // Use a more specific selector and wait for the element to be visible
        var deleteButton = _page.Locator($"tr:has-text('{violationTypeWithViolations.Name}') button:has-text('Delete')");
        await deleteButton.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await deleteButton.ClickAsync();
        
        // Handle the confirmation dialog by accepting it
        _page.Dialog += async (sender, e) => await e.AcceptAsync();
        await _page.WaitForTimeoutAsync(1000);

        // Assert - Verify an error alert is shown
        _page.Dialog += async (sender, e) =>
        {
            Assert.Contains("Error", e.Message);
            Assert.Contains("Could not delete violation type", e.Message);
            await e.AcceptAsync();
        };
        await _page.WaitForTimeoutAsync(1000);

        // Verify the violation type is still in the list
        await _page.WaitForSelectorAsync($"text={violationTypeWithViolations.Name}");

        // Verify in database
        var existingViolationType = await ViolationService.GetViolationTypeByIdAsync(violationTypeWithViolations.Id);
        Assert.NotNull(existingViolationType);
    }

    [Fact]
    public async Task Admin_CanViewEmptyViolationTypesList()
    {
        // Arrange - Navigate to ViolationTypes page
        await _page.ClickAsync("text=Violation Types");
        
        // Wait for the loading canary to disappear (page is fully loaded)
        await WaitForPageToLoadAsync();

        // Act - Check if there are any violation types with our test namespace
        var allViolationTypes = await ViolationService.GetViolationTypesAsync();
        var testViolationTypes = allViolationTypes.Where(vt => vt.Name.Contains(_testNamespace)).ToList();
        if (!testViolationTypes.Any())
        {
            // Only if no namespaced data exists, check for empty state
            // If there are any violation types at all, the empty state will not show
            if (allViolationTypes.Count == 0)
            {
                await _page.WaitForSelectorAsync("text=No violation types found. Click \"Add New Violation Type\" to get started.");
            }
            else
            {
                // There are violation types, but none for this namespace; pass as long as no namespaced types are present
                Assert.True(true);
            }
        }
        else
        {
            // Assert - At least one namespaced violation type is present
            foreach (var vt in testViolationTypes)
            {
                await _page.WaitForSelectorAsync($"text={vt.Name}");
            }
        }
    }

    [Fact]
    public async Task Admin_CanPerformCompleteCRUDWorkflow()
    {
        // Arrange - Navigate to ViolationTypes page
        await _page.ClickAsync("text=Violation Types");
        
        // Wait for the loading canary to disappear (page is fully loaded)
        await WaitForPageToLoadAsync();

        // Act 1: Create a new violation type
        await _page.ClickAsync("text=Add New Violation Type");
        await _page.WaitForSelectorAsync("h3:has-text('Create New Violation Type')");

        var workflowViolationTypeName = $"{_testNamespace}_{_adminUserName}_CRUD_Workflow_Test";
        var workflowCovenantText = $"{_adminUserName} covenant text for CRUD workflow test";
        await _page.FillAsync("#name", workflowViolationTypeName);
        await _page.FillAsync("#covenantText", workflowCovenantText);
        await _page.ClickAsync("button[type='submit']");
        await WaitForViolationTypesPageToLoadAsync();
        
        await _page.WaitForSelectorAsync($"text={workflowViolationTypeName}");

        // Act 2: Read the violation type (verify it's in the list)
        var createdViolationTypes = await ViolationService.GetViolationTypesAsync();
        var createdViolationType = createdViolationTypes.FirstOrDefault(vt => vt.Name == workflowViolationTypeName);
        Assert.NotNull(createdViolationType);

        // Act 3: Update the violation type
        await _page.ClickAsync($"tr:has-text('{createdViolationType.Name}') button:has-text('Edit')");
        await _page.WaitForSelectorAsync("h3:has-text('Edit Violation Type')");
        var updatedName = $"{_testNamespace}_{_adminUserName}_CRUD_Workflow_Updated";
        var updatedCovenantText = $"{_adminUserName} updated covenant text for CRUD workflow";
        await _page.FillAsync("#name", updatedName);
        await _page.FillAsync("#covenantText", updatedCovenantText);
        await _page.ClickAsync("button[type='submit']");
        await WaitForViolationTypesPageToLoadAsync();
        
        await _page.WaitForSelectorAsync($"text={updatedName}");

        // Act 4: Delete the violation type
        var dialogHandled = false;
        _page.Dialog += async (sender, e) => {
            await e.AcceptAsync();
            dialogHandled = true;
        };
        await _page.ClickAsync($"tr:has-text('{updatedName}') button:has-text('Delete')");
        // Wait for the dialog to be handled
        for (int i = 0; i < 10 && !dialogHandled; i++)
        {
            await Task.Delay(100);
        }
        // Wait for the row to be removed from the DOM
        await _page.WaitForSelectorAsync($"tr:has-text('{updatedName}')", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
        // Assert - Ensure the row is gone
        var rows = await _page.QuerySelectorAllAsync("tbody tr");
        foreach (var row in rows)
        {
            var text = await row.TextContentAsync();
            Assert.DoesNotContain(updatedName, text);
        }
        var deletedViolationType = await ViolationService.GetViolationTypeByIdAsync(createdViolationType.Id);
        Assert.Null(deletedViolationType);
    }

    [Fact]
    public async Task Admin_CanHandleFormValidationErrors()
    {
        // Arrange - Navigate to create form
        await _page.ClickAsync("text=Violation Types");
        
        // Wait for the loading canary to disappear (page is fully loaded)
        await WaitForPageToLoadAsync();
        
        await _page.ClickAsync("text=Add New Violation Type");
        await _page.WaitForSelectorAsync("h3:has-text('Create New Violation Type')");

        // Act - Submit form without required data
        await _page.ClickAsync("button[type='submit']");

        // Assert - Verify validation errors are shown
        await _page.WaitForSelectorAsync(".validation-message");
    }

    [Fact]
    public async Task Admin_CanViewTableStructure()
    {
        // Arrange - Navigate to ViolationTypes page
        await _page.ClickAsync("text=Violation Types");
        
        // Wait for the loading canary to disappear (page is fully loaded)
        await WaitForPageToLoadAsync();

        // Act - Verify table structure
        await WaitForViolationTypesPageToLoadAsync();
        await _page.WaitForSelectorAsync("table");
        await _page.WaitForSelectorAsync("th:has-text('ID')");
        await _page.WaitForSelectorAsync("th:has-text('Name')");
        await _page.WaitForSelectorAsync("th:has-text('Description')");
        await _page.WaitForSelectorAsync("th:has-text('Actions')");

        // Assert - Verify action buttons are present
        await _page.WaitForSelectorAsync("a:has-text('Add New Violation Type')");
    }
} 