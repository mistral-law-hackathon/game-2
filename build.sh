#!/usr/bin/env bash
# Headless WebGL build into ./webgl (needs an activated Unity license).
set -euo pipefail
UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.0.84f1/Editor/Unity}"
cd "$(dirname "$0")"
"$UNITY" -batchmode -nographics -quit -projectPath . -buildTarget WebGL \
  -executeMethod BuildScript.BuildWebGL -logFile build.log
