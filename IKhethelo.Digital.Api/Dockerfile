FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /src
COPY IKhethelo.Digital.Api/global.json IKhethelo.Digital.Api/Directory.Build.props ./IKhethelo.Digital.Api/
COPY IKhethelo.Digital.Api/src/IKhethelo.Digital.Api/IKhethelo.Digital.Api.csproj ./IKhethelo.Digital.Api/src/IKhethelo.Digital.Api/
COPY IKhethelo.Digital.Api/src/IKhethelo.Digital.Infrastructure/IKhethelo.Digital.Infrastructure.csproj ./IKhethelo.Digital.Api/src/IKhethelo.Digital.Infrastructure/
COPY IKhethelo.Digital.Api/src/IKhethelo.Digital.Application/IKhethelo.Digital.Application.csproj ./IKhethelo.Digital.Api/src/IKhethelo.Digital.Application/
COPY IKhethelo.Digital.Api/src/IKhethelo.Digital.Domain/IKhethelo.Digital.Domain.csproj ./IKhethelo.Digital.Api/src/IKhethelo.Digital.Domain/
RUN dotnet restore IKhethelo.Digital.Api/src/IKhethelo.Digital.Api/IKhethelo.Digital.Api.csproj
COPY IKhethelo.Digital.Api/src/ ./IKhethelo.Digital.Api/src/
COPY contracts/ikhethelo-digital-api.openapi.yaml ./contracts/ikhethelo-digital-api.openapi.yaml
RUN dotnet publish IKhethelo.Digital.Api/src/IKhethelo.Digital.Api/IKhethelo.Digital.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production PORT=10000
EXPOSE 10000
COPY --from=build /out/ ./
USER $APP_UID
ENTRYPOINT ["dotnet", "IKhethelo.Digital.Api.dll"]
