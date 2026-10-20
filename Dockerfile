FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /workspace
COPY Framework/Directory.Build.props Framework/Directory.Packages.props Framework/
COPY Framework/src/ Framework/src/
COPY AI-Framework/Directory.Build.props AI-Framework/Directory.Packages.props AI-Framework/
COPY AI-Framework/src/AIFramework.Core/ AI-Framework/src/AIFramework.Core/
COPY BusinessSuite/Directory.Build.props BusinessSuite/Directory.Packages.props BusinessSuite/
COPY BusinessSuite/src/ BusinessSuite/src/
RUN dotnet publish BusinessSuite/src/BusinessSuite.Api/BusinessSuite.Api.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /out/ ./
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "BusinessSuite.Api.dll"]
