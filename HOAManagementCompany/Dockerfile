FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY HOAManagementCompany/HOAManagementCompany.csproj HOAManagementCompany/
RUN dotnet restore HOAManagementCompany/HOAManagementCompany.csproj
COPY HOAManagementCompany/ ./HOAManagementCompany/
WORKDIR /src/HOAManagementCompany
RUN dotnet publish HOAManagementCompany.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
# Pull Debian security fixes newer than the base image (e.g. pcre2, perl CVEs flagged by the
# Trivy image scan) without waiting for Microsoft to republish aspnet:9.0.
RUN apt-get update \
    && apt-get upgrade -y --no-install-recommends \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HOAManagementCompany.dll"]
