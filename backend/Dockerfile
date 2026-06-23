FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY Nexus.Orders.sln ./
COPY src/Nexus.Orders.Api/*.csproj ./src/Nexus.Orders.Api/
COPY src/Nexus.Orders.Application/*.csproj ./src/Nexus.Orders.Application/
COPY src/Nexus.Orders.Infrastructure/*.csproj ./src/Nexus.Orders.Infrastructure/
RUN dotnet restore src/Nexus.Orders.Api/Nexus.Orders.Api.csproj

COPY src/ ./src/
RUN dotnet publish src/Nexus.Orders.Api/Nexus.Orders.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish ./

EXPOSE 5000
ENTRYPOINT ["dotnet", "Nexus.Orders.Api.dll"]
