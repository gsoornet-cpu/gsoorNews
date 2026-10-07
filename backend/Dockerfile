FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Jusoor.sln .
COPY src/Jusoor.Domain/Jusoor.Domain.csproj src/Jusoor.Domain/
COPY src/Jusoor.Application/Jusoor.Application.csproj src/Jusoor.Application/
COPY src/Jusoor.Infrastructure/Jusoor.Infrastructure.csproj src/Jusoor.Infrastructure/
COPY src/Jusoor.Api/Jusoor.Api.csproj src/Jusoor.Api/
COPY tests/Jusoor.Domain.UnitTests/Jusoor.Domain.UnitTests.csproj tests/Jusoor.Domain.UnitTests/
COPY tests/Jusoor.Infrastructure.UnitTests/Jusoor.Infrastructure.UnitTests.csproj tests/Jusoor.Infrastructure.UnitTests/
COPY tests/Jusoor.Application.UnitTests/Jusoor.Application.UnitTests.csproj tests/Jusoor.Application.UnitTests/
COPY tests/Jusoor.Api.IntegrationTests/Jusoor.Api.IntegrationTests.csproj tests/Jusoor.Api.IntegrationTests/
RUN dotnet restore Jusoor.sln

COPY . .
RUN dotnet publish src/Jusoor.Api/Jusoor.Api.csproj -c Release -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Runs as a non-root user — never run production containers as root.
RUN useradd --create-home --shell /bin/bash jusoor \
    && chown -R jusoor:jusoor /app
USER jusoor

EXPOSE 8080
ENTRYPOINT ["dotnet", "Jusoor.Api.dll"]
