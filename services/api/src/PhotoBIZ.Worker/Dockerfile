FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY PhotoBIZ.slnx ./
COPY .editorconfig ./
COPY Directory.Build.props ./
COPY global.json ./
COPY services/api/src/PhotoBIZ.Api/PhotoBIZ.Api.csproj services/api/src/PhotoBIZ.Api/
COPY services/api/src/PhotoBIZ.Worker/PhotoBIZ.Worker.csproj services/api/src/PhotoBIZ.Worker/

RUN dotnet restore services/api/src/PhotoBIZ.Worker/PhotoBIZ.Worker.csproj

COPY services/api/src/PhotoBIZ.Api/ services/api/src/PhotoBIZ.Api/
COPY services/api/src/PhotoBIZ.Worker/ services/api/src/PhotoBIZ.Worker/

RUN dotnet publish services/api/src/PhotoBIZ.Worker/PhotoBIZ.Worker.csproj --configuration Release --output /app/publish --no-restore -p:ErrorOnDuplicatePublishOutputFiles=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PhotoBIZ.Worker.dll"]
