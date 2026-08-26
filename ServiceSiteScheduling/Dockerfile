FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /build
COPY HIP.csproj .
RUN dotnet restore
COPY . .
# Build with ASSERTIONS=true for an image intended for integration testing:
# same optimisation and same plans as the default build, but Debug.Assert stays
# live so internal invariant violations surface. Publish those under a separate
# tag - never as the release tag, since a failing assertion aborts the process.
ARG ASSERTIONS=false
# Empty by default, so a bare "docker build" with no --build-arg (i.e. not
# going through docker-push.sh/docker-push-edge.sh) still gets a sensible
# version: whatever's checked into HIP.csproj's <Version>. When docker-push.sh
# passes VERSION (the same value it also puts in the LABEL below), -p:Version
# overrides that at publish time, so Program.cs's startup "HIP {Version}" line
# - read from the built assembly's AssemblyInformationalVersionAttribute, not
# from the LABEL, which a running container can't introspect - actually
# matches what the image claims to be. Previously it never did: this ARG only
# reached the LABEL, so every image printed whatever version happened to be
# committed in HIP.csproj at build time, coincidentally correct for ordinary
# releases and silently wrong for anything that overrides VERSION, like edge
# builds (see docker-push-edge.sh).
ARG VERSION=
RUN dotnet publish -c Release -p:Assertions=${ASSERTIONS} ${VERSION:+-p:Version=${VERSION}} --no-restore -o /publish

FROM mcr.microsoft.com/dotnet/runtime:8.0

ARG VERSION=0.0.0
LABEL org.opencontainers.image.source="https://github.com/Robust-Rail-NL/robust-rail-solver" \
      org.opencontainers.image.description="HIP solver" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.licenses="MIT"

WORKDIR /app
RUN mkdir -p /app/tmp_plans && chmod 777 /app/tmp_plans
COPY --from=build /publish .
# The no-config run (see Program.cs) reads "../example_kleine_binckhorst",
# relative to the working directory it expects to run from — ServiceSiteScheduling/
# locally, /app here. "fixtures" is a named build context (passed via
# --build-context in docker-push.sh) pointing at the repo-root fixture
# directory, which sits outside this Dockerfile's own build context.
COPY --from=fixtures . /example_kleine_binckhorst
ENTRYPOINT ["dotnet", "ServiceSiteScheduling.dll"]
