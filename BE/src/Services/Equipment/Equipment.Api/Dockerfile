FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
WORKDIR /source
COPY . .
RUN dotnet restore src/Services/Equipment/Equipment.Api/Equipment.Api.csproj --locked-mode
RUN dotnet publish src/Services/Equipment/Equipment.Api/Equipment.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0.13
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080 8081
ENTRYPOINT ["dotnet", "Equipment.Api.dll"]
