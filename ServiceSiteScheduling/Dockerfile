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
ENTRYPOINT ["dotnet", "ServiceSiteScheduling.dll"]
