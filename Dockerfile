FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/DomainProvisioningService.Worker/DomainProvisioningService.Worker.csproj", "DomainProvisioningService.Worker/"]
COPY ["src/DomainProvisioningService.Application/DomainProvisioningService.Application.csproj", "DomainProvisioningService.Application/"]
COPY ["src/DomainProvisioningService.Domain/DomainProvisioningService.Domain.csproj", "DomainProvisioningService.Domain/"]
COPY ["src/DomainProvisioningService.Infrastructure/DomainProvisioningService.Infrastructure.csproj", "DomainProvisioningService.Infrastructure/"]

RUN dotnet restore "DomainProvisioningService.Worker/DomainProvisioningService.Worker.csproj"

# Copy all source files
COPY src/ .

# Build
WORKDIR "/src/DomainProvisioningService.Worker"
RUN dotnet build "DomainProvisioningService.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DomainProvisioningService.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DomainProvisioningService.Worker.dll"]
