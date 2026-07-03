FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /build
COPY HIP.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release --no-restore -o /publish

FROM mcr.microsoft.com/dotnet/runtime:8.0

LABEL org.opencontainers.image.source="https://github.com/Robust-Rail-NL/robust-rail-solver" \
      org.opencontainers.image.description="HIP solver" \
      org.opencontainers.image.version="1.4.0" \
      org.opencontainers.image.licenses="MIT"

WORKDIR /app
COPY --from=build /publish .
ENTRYPOINT ["dotnet", "ServiceSiteScheduling.dll"]
