# Multi-stage build: compile with the .NET SDK image, run on the smaller ASP.NET runtime image.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first (cached layer) so code changes don't re-download packages.
COPY PawConnect.sln ./
COPY src/PawConnect.Core/PawConnect.Core.csproj src/PawConnect.Core/
COPY src/PawConnect.Infrastructure/PawConnect.Infrastructure.csproj src/PawConnect.Infrastructure/
COPY src/PawConnect.Web/PawConnect.Web.csproj src/PawConnect.Web/
RUN dotnet restore src/PawConnect.Web/PawConnect.Web.csproj

COPY src/ src/
RUN dotnet publish src/PawConnect.Web/PawConnect.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
# Run as the non-root user that ships with the .NET 8 images.
USER app
# Listen on $PORT when the host provides one (Render uses 10000), otherwise 8080.
CMD ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} exec dotnet PawConnect.Web.dll"]
