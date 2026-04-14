FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["ArkhamChangeRequest.csproj", "./"]
RUN dotnet restore "ArkhamChangeRequest.csproj"

COPY . .
RUN dotnet publish "ArkhamChangeRequest.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "ArkhamChangeRequest.dll"]
