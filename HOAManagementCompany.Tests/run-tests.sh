#!/bin/bash

# Integration Test Runner for HOAManagementCompany
# This script runs the xUnit integration tests against the Docker PostgreSQL database

echo "🚀 Starting HOAManagementCompany Integration Tests"
echo "=================================================="

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "❌ Docker is not running. Please start Docker and try again."
    exit 1
fi

# Check if PostgreSQL container is running
if ! docker ps | grep -q "my_app_postgres_db"; then
    echo "❌ PostgreSQL container is not running. Starting it now..."
    docker-compose up -d postgres-db
    
    # Wait for PostgreSQL to be ready
    echo "⏳ Waiting for PostgreSQL to be ready..."
    sleep 10
fi

# Build the test project
echo "🔨 Building test project..."
dotnet build HOAManagementCompany.Tests/HOAManagementCompany.Tests.csproj

if [ $? -ne 0 ]; then
    echo "❌ Build failed. Please fix the build errors and try again."
    exit 1
fi

# Run the tests
echo "🧪 Running integration tests..."
dotnet test HOAManagementCompany.Tests/HOAManagementCompany.Tests.csproj --verbosity normal --logger "console;verbosity=detailed"

# Check test results
if [ $? -eq 0 ]; then
    echo "✅ All tests passed!"
else
    echo "❌ Some tests failed. Check the output above for details."
    exit 1
fi

echo "🏁 Integration tests completed!" 