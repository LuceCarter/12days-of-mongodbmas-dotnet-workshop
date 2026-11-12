#!/usr/bin/env bash
# Runs once, after the dev container is created.
set -euo pipefail

echo "🎄 Setting up Santa's Workshop..."

cd /workspace

echo "📦 Restoring NuGet packages..."
dotnet restore

echo "🔍 Checking your environment..."
# Don't fail the whole setup if the doctor finds a problem — it prints
# instructions, and the learner can fix things and run it again.
dotnet run --project tools/doctor || true

cat <<'BANNER'

  ✨ Setup complete.

  Next:
    1. Read part1-inventory/README.md
    2. Run the tests (they should be red to start with):
         dotnet test part1-inventory/Tests
    3. Re-run the environment check any time:
         dotnet run --project tools/doctor

BANNER
