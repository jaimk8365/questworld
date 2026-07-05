#!/bin/bash
# QuestWorld one-command deploy: builds the game and publishes it to
# GitHub Pages. Usage:  ./deploy.sh
set -euo pipefail
cd "$(dirname "$0")"
export PATH="$HOME/.dotnet:$PATH"

REPO_URL=$(git remote get-url origin 2>/dev/null) || {
  echo "No 'origin' remote set. Run the one-time GitHub setup first."; exit 1;
}

echo "==> Running logic tests…"
dotnet run --project tests >/dev/null && echo "    tests passed"

echo "==> Building…"
QW_BASE="${QW_BASE:-/questworld/}" npm run build >/dev/null

STAMP=$(date +%Y%m%d%H%M%S)
sed -i '' "s/__QW_BUILD__/$STAMP/" dist/sw.js
echo "    build $STAMP"

echo "==> Publishing to GitHub Pages…"
rm -rf dist/.git
git -C dist init -q -b gh-pages
git -C dist add -A
git -C dist -c user.name="QuestWorld Deploy" -c user.email="deploy@questworld.local" commit -qm "Deploy $STAMP"
git -C dist push -qf "$REPO_URL" gh-pages
rm -rf dist/.git

echo "==> Done! The site updates in about a minute."
