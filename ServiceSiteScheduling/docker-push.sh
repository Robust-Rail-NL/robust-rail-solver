#!/usr/bin/env bash
# Build and push the multi-arch HIP solver image to ghcr.io.
#
# The version is read from HIP.csproj's <Version> element (the single
# source of truth — use bump-version.sh to change it) and passed into the
# image as a build-arg, so the Dockerfile LABEL never needs a separate edit.
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
set -euo pipefail

IMAGE="ghcr.io/robust-rail-nl/hip"
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
    --push \
    .

# Never tagged :latest, whatever the version shape — :latest is what someone
# gets when they ask for the solver without thinking about it, and that should
# never be a build that aborts on a failed assertion.
docker buildx build \
    --builder "$BUILDER_NAME" \
    --platform linux/amd64,linux/arm64 \
    --build-arg "VERSION=$VERSION" \
    --build-arg "ASSERTIONS=true" \
    --build-context fixtures=../example_kleine_binckhorst \
    -t "$IMAGE:$VERSION-assert" \
    --push \
    .
