#!/usr/bin/env bash
# Bump the HIP version in HIP.csproj's <Version> element — the single
# source of truth used by docker-push.sh for the image tag and label.
set -euo pipefail

CSPROJ="HIP.csproj"
CURRENT=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$CSPROJ")
[[ -n "$CURRENT" ]] || { echo "Could not read <Version> from $CSPROJ" >&2; exit 1; }

usage() {
    echo "Usage: $0 <major|minor|patch|prerelease|X.Y.Z[-suffix]>" >&2
    echo "Current version: $CURRENT" >&2
    exit 1
}

[[ $# -eq 1 ]] || usage

RELEASE="${CURRENT%%-*}"
IFS='.' read -r MAJOR MINOR PATCH <<< "$RELEASE"
if [[ "$CURRENT" == *-* ]]; then
    PRE="${CURRENT#*-}"
else
    PRE=""
fi

case "$1" in
    major) NEW="$((MAJOR + 1)).0.0" ;;
    minor) NEW="$MAJOR.$((MINOR + 1)).0" ;;
    patch) NEW="$MAJOR.$MINOR.$((PATCH + 1))" ;;
    prerelease)
        if [[ "$PRE" =~ ^([A-Za-z]+)\.([0-9]+)$ ]]; then
            NEW="$RELEASE-${BASH_REMATCH[1]}.$((${BASH_REMATCH[2]} + 1))"
        else
            NEW="$RELEASE-alpha.1"
        fi
        ;;
    [0-9]*.[0-9]*.[0-9]*) NEW="$1" ;;
    *) usage ;;
esac

sed -i "s#<Version>$CURRENT</Version>#<Version>$NEW</Version>#" "$CSPROJ"
echo "Bumped version: $CURRENT -> $NEW"
