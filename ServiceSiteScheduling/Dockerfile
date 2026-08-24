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
RUN dotnet publish -c Release -p:Assertions=${ASSERTIONS} --no-restore -o /publish

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
