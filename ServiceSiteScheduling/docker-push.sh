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
#
# Refuses to run unless the working tree is clean and HEAD is the commit
# tagged v<version> (as created by bump-version.sh), with that tag pushed to
# origin. Pass -f to skip these checks.
set -euo pipefail
cd "$(dirname "$0")"

# BEGIN SHARED BLOCK: release-flags (identical in every repo's push scripts; edit all copies — checked by robust-rail-general/scripts/check_push_script_sync.py)
FORCE=0
while getopts f opt; do
    case $opt in
        f) FORCE=1 ;;
        *) echo "Usage: $0 [-f]" >&2; exit 2 ;;
    esac
done
# END SHARED BLOCK: release-flags

IMAGE="ghcr.io/robust-rail-nl/hip"
CACHE_REF="$IMAGE:buildcache"
BUILDER_NAME="robust-rail-builder"

VERSION=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' HIP.csproj)
[[ -n "$VERSION" ]] || { echo "Could not read <Version> from HIP.csproj" >&2; exit 1; }

# BEGIN SHARED BLOCK: release-guards (identical in every repo's push scripts; edit all copies — checked by robust-rail-general/scripts/check_push_script_sync.py)
# A release image is permanent, and :$VERSION / :latest is what consumers
# pull, so only publish a commit that bump-version.sh has tagged v$VERSION
# and whose tag is on origin (which also means the commit itself is pushed).
# The working tree must be clean too: the docker build uses the working
# tree, not the commit. -f skips all of these checks.
TAG="v$VERSION"
if [[ $FORCE -eq 1 ]]; then
    echo "WARNING: -f given, skipping the clean-tree / pushed-tag checks for $TAG." >&2
else
    if [[ -n "$(git status --porcelain)" ]]; then
        echo "Refusing to publish $VERSION from a dirty working tree — commit or stash first (or use -f)." >&2
        git status --short >&2
        exit 1
    fi
    TAG_SHA=$(git rev-parse -q --verify "refs/tags/$TAG^{commit}") || {
        echo "Refusing to publish $VERSION: no local tag $TAG — run bump-version.sh, or use -f." >&2
        exit 1
    }
    if [[ "$TAG_SHA" != "$(git rev-parse HEAD)" ]]; then
        echo "Refusing to publish $VERSION: HEAD ($(git rev-parse --short HEAD)) is not the commit tagged $TAG ($(git rev-parse --short "$TAG_SHA")) — checkout $TAG, or use -f." >&2
        exit 1
    fi
    # Annotated tags list the tagged commit on the peeled ^{} line; a
    # lightweight tag has only the plain line.
    REMOTE_SHA=$(git ls-remote origin "refs/tags/$TAG^{}" | cut -f1 || true)
    [[ -n "$REMOTE_SHA" ]] || REMOTE_SHA=$(git ls-remote origin "refs/tags/$TAG" | cut -f1 || true)
    if [[ "$REMOTE_SHA" != "$TAG_SHA" ]]; then
        echo "Refusing to publish $VERSION: tag $TAG is not on origin at $(git rev-parse --short "$TAG_SHA") (or origin is unreachable) — git push origin $TAG, or use -f." >&2
        exit 1
    fi
fi
# END SHARED BLOCK: release-guards

# BEGIN SHARED BLOCK: ghcr-login (identical in every repo's push scripts; edit all copies — checked by robust-rail-general/scripts/check_push_script_sync.py)
docker login ghcr.io
# Only the push needs auth; ghcr.io/robust-rail-nl is public for reads. Log
# out again once this script exits (success or failure) rather than leaving
# the credential sitting in the credential store, where every unrelated
# docker pull against ghcr.io (e.g. run_experiment.py's) ends up decrypting
# it for no reason.
trap 'docker logout ghcr.io' EXIT
# END SHARED BLOCK: ghcr-login

TAGS=(-t "$IMAGE:$VERSION")
if [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    TAGS+=(-t "$IMAGE:latest")
fi

# BEGIN SHARED BLOCK: buildx-builder (identical in every repo's push scripts; edit all copies — checked by robust-rail-general/scripts/check_push_script_sync.py)
if ! docker buildx inspect "$BUILDER_NAME" >/dev/null 2>&1; then
    docker buildx create --name "$BUILDER_NAME" --driver docker-container --driver-opt network=host
fi
# END SHARED BLOCK: buildx-builder

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
