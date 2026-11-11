# Build image for the CurioDistrict API and Worker.
#
# One image, two roles — the compose file picks which one to run via the
# per-service `working_dir` and `command`. The image holds the published
# binaries and the ASP.NET runtime only: no credential, no connection string
# and no environment value is ever baked into a layer (story 004 AC2).
# `backend/` is the build context; `backend/.dockerignore` keeps `.env` and
# build artifacts out of every layer.
#
# The aspnet image carries both the .NET runtime and ASP.NET Core, so the
# Worker (Microsoft.NET.Sdk.Worker) runs on it unchanged.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the project files first so the restore layer stays cached across code
# edits. The projects are restored individually rather than through the .sln:
# the solution file writes project paths with Windows separators, which
# MSBuild on Linux does not read the same way.
COPY Domain/CurioDistrict.Domain.csproj Domain/
COPY Config/CurioDistrict.Config/CurioDistrict.Config.csproj Config/CurioDistrict.Config/
COPY Data/CurioDistrict.Data.csproj Data/
COPY API/CurioDistrict.API.csproj API/
COPY Worker/CurioDistrict.Worker.csproj Worker/

RUN dotnet restore API/CurioDistrict.API.csproj \
    && dotnet restore Worker/CurioDistrict.Worker.csproj

COPY . ./
RUN dotnet publish API/CurioDistrict.API.csproj \
        -c Release \
        -o /out/api \
        --no-restore \
    && dotnet publish Worker/CurioDistrict.Worker.csproj \
        -c Release \
        -o /out/worker \
        --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# curl powers the api service's compose healthcheck; the apt lists are
# removed so the metadata never becomes its own layer.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# The two roles live in separate directories: publishing both into one
# directory would overwrite the shared dependency files.
WORKDIR /app
COPY --from=build /out/api ./api
COPY --from=build /out/worker ./worker

# The entrypoint is the dotnet host; the compose file sets the per-service
# `command` (API vs Worker dll) and `working_dir`.
ENTRYPOINT ["dotnet"]
