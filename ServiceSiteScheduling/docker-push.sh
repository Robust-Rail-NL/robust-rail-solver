#!/usr/bin/env bash
# Build and push the multi-arch HIP solver image to ghcr.io.
#
# The version is read from HIP.csproj's <Version> element (the single
# source of truth — use bump-version.sh to change it) and passed into the
# image as a build-arg, so neither the Dockerfile LABEL nor the published
# binary's own AssemblyInformationalVersionAttribute (what Program.cs prints
# at startup) needs a separate edit — see the Dockerfile's VERSION ARG.
#
# See docker-push-edge.sh for the floating :edge tag published off the edge
# branch — a different script, not a flag here, since it computes its version
# string differently (a date+commit suffix, not HIP.csproj's <Version>) and
# skips the -assert build entirely. See CONTRIBUTING.md for the edge channel.
#
# The :latest tag is only applied to final X.Y.Z releases. Prerelease
# versions (e.g. 2.0.0-rc.1) are pushed under their own tag only, so they
# never shadow the current stable image.
#
# Two images are pushed per version: $VERSION, and $VERSION-assert built with
# -p:Assertions=true (Release optimisation plus the DEBUG symbol, so Debug.
# Assert survives — see HIP.csproj). Both go out together so the assert tag
# cannot silently fail to exist, which is how the evaluator's equivalent tag
# came to be referenced by the pipeline for a whole release without ever
# having been built.
#
# The assert image is deliberately NOT what the pipeline runs. The solver is a
# wall-clock-bounded local search, so an assertions build explores less of the
# neighbourhood in the same budget and returns different plans on any scenario
# that has not already converged — which would break comparison against the
# baseline. Its purpose is the opposite: soak testing, sweeping seeds looking
# for an invariant violation, which is how the intermittent PlanGraph/Parking
# failure in Robust-Rail-NL/robust-rail-solver#11 would get pinned down.
#
# Requires a buildx builder using the "docker-container" driver with
# network=host. The default driver runs the BuildKit container in an
# isolated network namespace whose DNS resolution can fail to reach
# private/LAN DNS servers (seen as: "docker build" works, "docker buildx
# build" times out resolving mcr.microsoft.com). network=host makes the
# builder share the host's network stack, avoiding that failure mode.
#
# BUILDER_NAME is shared with sibling Robust-Rail-NL projects (e.g.
# robust-rail-evaluator) that need the same multi-arch/network=host setup
# — a buildx builder isn't tied to a specific repo or Dockerfile.
#
# --cache-to/--cache-from push and pull the build cache through a dedicated
# ":buildcache" tag on the same image (see robust-rail-planner's docker-push.sh
# for the mechanism; ghcr.io/robust-rail-nl is public, so this costs no
# storage/bandwidth quota). This mainly benefits `dotnet restore` here, not
# `dotnet publish` - VERSION is baked directly into the publish command
# (-p:Version=${VERSION}), so that step's cache key changes every release
# regardless of source, the same limitation robust-rail-evaluator has for its
# compile step. Unlike the evaluator, no ccache-equivalent cache mount has
# been added for that here - MSBuild's incremental build state (obj/) would
# be the analogous fix, but wasn't part of what was asked for.
set -euo pipefail
cd "$(dirname "$0")"

docker login ghcr.io

IMAGE="ghcr.io/robust-rail-nl/hip"
CACHE_REF="$IMAGE:buildcache"
BUILDER_NAME="robust-rail-builder"

VERSION=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' HIP.csproj)
[[ -n "$VERSION" ]] || { echo "Could not read <Version> from HIP.csproj" >&2; exit 1; }

TAGS=(-t "$IMAGE:$VERSION")
if [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    TAGS+=(-t "$IMAGE:latest")
fi

if ! docker buildx inspect "$BUILDER_NAME" >/dev/null 2>&1; then
    docker buildx create --name "$BUILDER_NAME" --driver docker-container --driver-opt network=host
fi

docker buildx build \
    --builder "$BUILDER_NAME" \
    --platform linux/amd64,linux/arm64 \
    --build-arg "VERSION=$VERSION" \
    --build-context fixtures=../example_kleine_binckhorst \
    "${TAGS[@]}" \
    --cache-to "type=registry,ref=$CACHE_REF,mode=max" \
    --cache-from "type=registry,ref=$CACHE_REF" \
    --push \
    .

TAGS=(-t "$IMAGE:$VERSION-assert")
if [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    TAGS+=(-t "$IMAGE:assert")
fi

# Never tagged :latest, whatever the version shape — :latest is what someone
# gets when they ask for the solver without thinking about it, and that should
# never be a build that aborts on a failed assertion.
docker buildx build \
    --builder "$BUILDER_NAME" \
    --platform linux/amd64,linux/arm64 \
    --build-arg "VERSION=$VERSION" \
    --build-arg "ASSERTIONS=true" \
    --build-context fixtures=../example_kleine_binckhorst \
    "${TAGS[@]}" \
    --cache-to "type=registry,ref=$CACHE_REF,mode=max" \
    --cache-from "type=registry,ref=$CACHE_REF" \
    --push \
    .
