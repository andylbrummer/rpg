#!/usr/bin/env bash
#
# Deterministic local e2e harness.
#
# The Playwright config reuses a long-lived dotnet RPC.Host locally (reuseExistingServer when
# !CI). A host kept alive across many back-to-back full runs accumulates resource/GC pressure
# in WSL2 and per-test time degrades. This wrapper guarantees every full-suite run starts
# against a FRESH backend: it kills any stale RPC.Host (by port and by process name) before
# invoking Playwright, which then boots a brand-new host via its webServer config.
#
# Usage:
#   npm run test:e2e:fresh            # full suite on a fresh host
#   npm run test:e2e:fresh -- keybindings.spec.ts   # subset, still fresh host
#
set -euo pipefail

E2E_PORT="${E2E_PORT:-19421}"

kill_stale_host() {
  # Kill whatever owns the e2e port (the previous reused host), if anything.
  if command -v fuser >/dev/null 2>&1; then
    fuser -k "${E2E_PORT}/tcp" >/dev/null 2>&1 || true
  fi
  # Belt-and-suspenders: kill any lingering RPC.Host dotnet process.
  pkill -f 'RPC.Host' >/dev/null 2>&1 || true
  # Give the OS a moment to release the port before Playwright tries to bind it.
  sleep 1
}

echo "[e2e-fresh] killing any stale RPC.Host on :${E2E_PORT} ..."
kill_stale_host

echo "[e2e-fresh] starting Playwright against a fresh backend ..."
cd "$(dirname "$0")/.."
exec npx playwright test "$@"
