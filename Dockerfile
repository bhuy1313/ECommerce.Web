FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ECommerce.Web/ECommerce.Web.csproj ECommerce.Web/
RUN dotnet restore ECommerce.Web/ECommerce.Web.csproj
COPY . .
RUN dotnet publish ECommerce.Web/ECommerce.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-10000} dotnet ECommerce.Web.dll"]
