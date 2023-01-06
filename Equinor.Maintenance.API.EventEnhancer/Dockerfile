FROM mcr.microsoft.com/dotnet/sdk:7.0-bullseye-slim AS build-env
WORKDIR /publish

COPY ./*.csproj .

RUN  dotnet restore . /property:Configuration=Release

COPY . .
RUN dotnet publish . -c Release --output ./out

FROM mcr.microsoft.com/dotnet/aspnet:7.0-bullseye-slim
WORKDIR /app
COPY --from=build-env /publish/out .

ENV ASPNETCORE_URLS "http://0.0.0.0:5005"
EXPOSE  5005
USER 9000

ENTRYPOINT ["dotnet", "Equinor.Maintenance.API.EventEnhancer.dll"]
