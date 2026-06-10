FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /build
COPY HIP.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release --no-restore -o /publish

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /publish .
ENTRYPOINT ["dotnet", "ServiceSiteScheduling.dll"]
