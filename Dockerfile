# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy project file
COPY FileSearchEngine/FileSearchEngine.csproj ./FileSearchEngine/
RUN dotnet restore FileSearchEngine/FileSearchEngine.csproj

# Copy all source files
COPY FileSearchEngine/ ./FileSearchEngine/
RUN dotnet build -c Release -o /app/build FileSearchEngine/FileSearchEngine.csproj

# Publish
RUN dotnet publish -c Release -o /app/publish FileSearchEngine/FileSearchEngine.csproj

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime

WORKDIR /app

# Set default search paths (can be overridden at runtime)
ENV SEARCH_PATHS=/data/documents:/data/desktop:/data/projects

# Copy published files from build stage
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "filesearch.dll"]
