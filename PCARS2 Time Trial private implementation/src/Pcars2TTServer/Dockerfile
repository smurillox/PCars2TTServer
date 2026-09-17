FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Pcars2TTServer/Pcars2TTServer.csproj", "src/Pcars2TTServer/"]
RUN dotnet restore "src/Pcars2TTServer/Pcars2TTServer.csproj"
COPY . .
RUN dotnet publish "src/Pcars2TTServer/Pcars2TTServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Pcars2TTServer.dll"]
