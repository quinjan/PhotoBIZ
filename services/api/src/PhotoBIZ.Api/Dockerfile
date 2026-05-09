FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY PhotoBIZ.slnx ./
COPY Directory.Build.props ./
COPY global.json ./
COPY services/api/src/PhotoBIZ.Api/PhotoBIZ.Api.csproj services/api/src/PhotoBIZ.Api/

RUN dotnet restore services/api/src/PhotoBIZ.Api/PhotoBIZ.Api.csproj

COPY services/api/src/PhotoBIZ.Api/ services/api/src/PhotoBIZ.Api/

RUN dotnet publish services/api/src/PhotoBIZ.Api/PhotoBIZ.Api.csproj --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PhotoBIZ.Api.dll"]
