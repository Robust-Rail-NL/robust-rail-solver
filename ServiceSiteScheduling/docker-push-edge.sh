#!/usr/bin/env bash
# Build and push the multi-arch HIP solver image to ghcr.io under the
# floating :edge tag — a fast, unvetted-fix channel alongside docker-push.sh's
# reviewed-release :$VERSION/:latest tags. See CONTRIBUTING.md for what edge
# is for and the branch flow it depends on.
#
# Must be run from the edge branch (checked below), since :edge is meant to
# always reflect whatever is currently on that branch — not whatever branch
# happened to be checked out locally.
#
# The image version embedded in the build (both the OCI label and the
# published binary's own AssemblyInformationalVersionAttribute — see the
# Dockerfile's VERSION ARG) is derived, not read from HIP.csproj as-is:
# <release>-edge+<date>.<short-sha>, e.g. 2.0.0-edge+20260826.a1b2c3d. The
# release portion is whatever's currently committed in HIP.csproj with any
# existing prerelease suffix of its own stripped, since edge is its own
# prerelease identifier, not one chained onto another. The date+sha are
# semver build metadata (after the +), so they never affect version
# precedence/sorting — this is for a human or a bug report to trace a running
# image back to an exact commit and day, not for tooling to compare against.
#
# No git tag is created per edge build: branch history is the record (see
# CONTRIBUTING.md for the one gap that leaves — a force-push/rebase of edge).
#
# No -assert counterpart, unlike docker-push.sh's release images: the -assert
# build exists for soak testing (sweeping seeds looking for an invariant
# violation), which presumes something stable enough to soak. edge changes on
# every push, so that presumption doesn't hold. Revisit if that turns out to
# be wanted anyway.
#
# Requires the same buildx builder as docker-push.sh — see its header comment
# for why (network=host, shared with sibling Robust-Rail-NL projects), and for
# why this also shares docker-push.sh's :buildcache ref — edge and release
# builds hit the same restore/publish layers, so each warms the cache for
# the other.
set -euo pipefail
cd "$(dirname "$0")"

docker login ghcr.io
# Only the push needs auth; ghcr.io/robust-rail-nl is public for reads. Log
# out again once this script exits (success or failure) rather than leaving
# the credential sitting in the credential store, where every unrelated
# docker pull against ghcr.io (e.g. run_experiment.py's) ends up decrypting
# it for no reason.
trap 'docker logout ghcr.io' EXIT

IMAGE="ghcr.io/robust-rail-nl/hip"
CACHE_REF="$IMAGE:buildcache"
BUILDER_NAME="robust-rail-builder"

BRANCH=$(git rev-parse --abbrev-ref HEAD)
if [[ "$BRANCH" != "edge" ]]; then
    echo "Refusing to publish :edge from branch '$BRANCH' — checkout edge first." >&2
    exit 1
fi

RELEASE=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' HIP.csproj)
[[ -n "$RELEASE" ]] || { echo "Could not read <Version> from HIP.csproj" >&2; exit 1; }
RELEASE="${RELEASE%%-*}"

EDGE_VERSION="$RELEASE-edge+$(date -u +%Y%m%d).$(git rev-parse --short HEAD)"

if ! docker buildx inspect "$BUILDER_NAME" >/dev/null 2>&1; then
    docker buildx create --name "$BUILDER_NAME" --driver docker-container --driver-opt network=host
fi

docker buildx build \
    --builder "$BUILDER_NAME" \
    --platform linux/amd64,linux/arm64 \
    --build-arg "VERSION=$EDGE_VERSION" \
    --build-context fixtures=../example_kleine_binckhorst \
    -t "$IMAGE:edge" \
    --cache-to "type=registry,ref=$CACHE_REF,mode=max" \
    --cache-from "type=registry,ref=$CACHE_REF" \
    --push \
    .

echo "Pushed $IMAGE:edge (version $EDGE_VERSION)"
