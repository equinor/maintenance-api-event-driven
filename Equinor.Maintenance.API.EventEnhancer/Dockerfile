FROM mcr.microsoft.com/dotnet/sdk:6.0-bullseye-slim-amd64 AS build-env
WORKDIR /publish

COPY ./*.csproj .

RUN  dotnet restore . /property:Configuration=Release

COPY . .
RUN dotnet publish . -c Release --output ./out

FROM mcr.microsoft.com/dotnet/aspnet:6.0-bullseye-slim-amd64
WORKDIR /app
COPY --from=build-env /publish/out .

ENV USE_ENVIRONMENT_CREDENTIAL true
ENV USE_RADIX_CERT_HEADERNAME true
EXPOSE  5000
USER 9000

ENTRYPOINT ["dotnet", "Equinor.Maintenance.API.EventEnhancer.dll"]
