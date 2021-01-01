#!/usr/bin/env bash
set -euo pipefail

dotnet run --project targets --no-launch-profile -- "$@"