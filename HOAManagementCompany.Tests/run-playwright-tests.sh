#!/bin/bash

# Playwright Test Runner for HOAManagementCompany
# This script runs the Playwright end-to-end tests against the running application

echo "🎭 Starting HOAManagementCompany Playwright Tests"
echo "=================================================="

# Check if the application is running
echo "🔍 Checking if the application is running..."
if ! curl -s -f http://localhost:5212/health > /dev/null 2>&1; then
    echo "❌ Application is not running on http://localhost:5212"
    echo "Please start the application first:"
    echo "dotnet run --project HOAManagementCompany/HOAManagementCompany.csproj"
    exit 1
fi

echo "✅ Application is running"

# Check if PostgreSQL container is running
if ! docker ps | grep -q "my_app_postgres_db"; then
    echo "❌ PostgreSQL container is not running. Starting it now..."
    cd ..
    docker-compose up -d postgres-db
    
    # Wait for PostgreSQL to be ready
    echo "⏳ Waiting for PostgreSQL to be ready..."
    sleep 10
    cd HOAManagementCompany.Tests
fi

# Build the test project
echo "🔨 Building test project..."
dotnet build HOAManagementCompany.Tests.csproj

if [ $? -ne 0 ]; then
    echo "❌ Build failed. Please fix the build errors and try again."
    exit 1
fi

# Run the Playwright tests
echo "🧪 Running Playwright tests..."
dotnet test HOAManagementCompany.Tests.csproj --filter "FullyQualifiedName~PlaywrightTests" --verbosity normal --logger "console;verbosity=detailed"

# Check test results
if [ $? -eq 0 ]; then
    echo "✅ All Playwright tests passed!"
else
    echo "❌ Some Playwright tests failed. Check the output above for details."
    exit 1
fi

echo "🏁 Playwright tests completed!" 