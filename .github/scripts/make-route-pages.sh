#!/usr/bin/env bash
# Give every route the landing page serves its own index.html.
#
#   .github/scripts/make-route-pages.sh <published wwwroot folder>
#
# GitHub Pages answers a path it doesn't have with a 404 *status*, then serves
# 404.html. People see the page (the Blazor router takes over) but crawlers and link
# previews see an error. A real file at /parts/1/index.html answers 200 instead.
#
# Each copy is the already-published index.html, so run this AFTER the <base href>
# rewrite. <base href> is an absolute path, so the copies work at any depth.
#
# The list of routes comes from tools/site-routes, which asks the app itself — see there.

set -euo pipefail

wwwroot="${1:?usage: make-route-pages.sh <published wwwroot folder>}"
[ -f "$wwwroot/index.html" ] || { echo "No index.html in $wwwroot" >&2; exit 1; }

cd "$(git rev-parse --show-toplevel)"

routes="$(dotnet run --project tools/site-routes --configuration Release)"

count=0
while IFS= read -r route; do
  [ -n "$route" ] || continue

  # The route names come from our own files, but a path built from text deserves a guard.
  if [[ ! "$route" =~ ^[A-Za-z0-9_-]+(/[A-Za-z0-9_-]+)*$ ]]; then
    echo "Refusing odd-looking route: '$route'" >&2
    exit 1
  fi

  mkdir -p "$wwwroot/$route"
  cp "$wwwroot/index.html" "$wwwroot/$route/index.html"
  count=$((count + 1))
done <<< "$routes"

echo "Wrote an index.html for $count routes."
