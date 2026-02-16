#!/bin/bash
# Build script for FoamScript - pulls latest, builds, and tests

set -e  # Exit on error

echo "=== Pulling latest changes ==="
git pull origin develop

echo ""
echo "=== Clean the project directory ==="
dotnet clean

echo ""
echo "=== Building Release configuration ==="
dotnet build --configuration Release

echo ""
echo "=== Running tests ==="
dotnet test --configuration Release

echo ""
echo "✓ Build and test complete!"
