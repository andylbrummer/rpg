#!/bin/bash
set -e

echo "=== Building The Reach ==="

# Build .NET backend
echo "Building .NET backend..."
cd src/engine
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
cd ../..

# Build frontend
echo "Building frontend..."
cd src/client
npm install
npm run build
cd ../..

echo "=== Build complete ==="
echo ""
echo "To run in development mode:"
echo "  Terminal 1: cd src/client && npm run dev"
echo "  Terminal 2: cd src/engine/RPC.Host && dotnet run -- --dev"
echo ""
echo "To run the desktop app:"
echo "  cd src/engine/RPC.Host && dotnet run"
