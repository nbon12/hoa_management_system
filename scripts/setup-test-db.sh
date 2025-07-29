#!/bin/bash

# Setup script for local test database
# This script helps set up a PostgreSQL database for running integration tests locally

set -e

echo "Setting up test database..."

# Install Entity Framework tools if not already installed
if ! command -v dotnet-ef &> /dev/null; then
    echo "Installing Entity Framework tools..."
    dotnet tool install --global dotnet-ef
    # Add to PATH if not already there
    export PATH="$HOME/.dotnet/tools:$PATH"
fi

# Check if PostgreSQL is running
if ! pg_isready -h localhost -p 5432 > /dev/null 2>&1; then
    echo "PostgreSQL is not running. Please start PostgreSQL first."
    echo "You can use Docker: docker run --name postgres-test -e POSTGRES_PASSWORD=postgres -e POSTGRES_USER=postgres -e POSTGRES_DB=test_hoa -p 5432:5432 -d postgres:15"
    exit 1
fi

# Create test database if it doesn't exist
echo "Creating test database..."
psql -h localhost -U postgres -c "CREATE DATABASE test_hoa;" 2>/dev/null || echo "Database test_hoa already exists"

# Run migrations
echo "Running database migrations..."
dotnet ef database update --project HOAManagementCompany --connection "Host=localhost;Port=5432;Database=test_hoa;Username=postgres;Password=postgres"

echo "Test database setup complete!"
echo ""
echo "To run tests with the test database, use:"
echo "export ConnectionStrings__DefaultConnection=\"Host=localhost;Port=5432;Database=test_hoa;Username=postgres;Password=postgres\""
echo "dotnet test HOAManagementCompany.Tests" 