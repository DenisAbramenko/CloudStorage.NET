FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src
COPY CloudStorage.API/CloudStorage.API.csproj CloudStorage.API/
RUN dotnet restore CloudStorage.API/CloudStorage.API.csproj

FROM restore AS build
COPY CloudStorage.API/ CloudStorage.API/
RUN dotnet build CloudStorage.API/CloudStorage.API.csproj -c Release --no-restore

FROM build AS publish
RUN dotnet publish CloudStorage.API/CloudStorage.API.csproj -c Release --no-restore --no-build -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=publish /app/publish .
RUN mkdir -p /app/App_Data/Uploads && chown -R $APP_UID:$APP_UID /app/App_Data
USER $APP_UID
ENTRYPOINT ["dotnet", "CloudStorage.API.dll"]
