FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore ./ASI.Basecode.WebApp/ASI.Basecode.WebApp.csproj

RUN dotnet publish ./ASI.Basecode.WebApp/ASI.Basecode.WebApp.csproj -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/out .

ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

ENTRYPOINT ["dotnet", "ASI.Basecode.WebApp.dll"]
