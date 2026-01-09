# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy solution and project files first for better layer caching
COPY TLDRkseid.sln ./
COPY TLDRkseid/TLDRkseid.csproj ./TLDRkseid/
COPY TLDRkseid.Tests/TLDRkseid.Tests.csproj ./TLDRkseid.Tests/

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY TLDRkseid/ ./TLDRkseid/
COPY TLDRkseid.Tests/ ./TLDRkseid.Tests/

# Run tests
RUN dotnet test --no-restore --verbosity normal

# Publish the main project
RUN dotnet publish TLDRkseid/TLDRkseid.csproj -c Release -o out --no-restore

# Stage 2: Runtime (using aspnet for health check web server)
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out .

# Expose health check port (Railway will set PORT env var)
EXPOSE 8080

ENTRYPOINT ["dotnet", "TLDRkseid.dll"]
