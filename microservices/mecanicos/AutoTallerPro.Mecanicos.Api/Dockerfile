FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props global.json ./
COPY microservices/mecanicos/ microservices/mecanicos/
RUN dotnet publish microservices/mecanicos/AutoTallerPro.Mecanicos.Api/AutoTallerPro.Mecanicos.Api.csproj -c Release -o /out -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /out/ ./
USER $APP_UID
ENTRYPOINT ["dotnet", "AutoTallerPro.Mecanicos.Api.dll"]
