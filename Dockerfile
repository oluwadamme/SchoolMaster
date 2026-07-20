# DockerFile: How to build and run your app on any platform\
# contains your app and everything it needs to run

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
ENV NUGET_FALLBACK_PACKAGES=""

# Copy csproj first for layer caching (only re-restores if dependencies change)
COPY SchoolMaster.csproj .
RUN dotnet restore SchoolMaster.csproj

# Copy everything else and publish
COPY . .
RUN dotnet publish SchoolMaster.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime (much smaller image)
#running environment container creates when it starts
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080

#instructions on how to start app
ENTRYPOINT ["dotnet", "SchoolMaster.dll"]
