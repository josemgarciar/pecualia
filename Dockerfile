FROM node:20-alpine AS frontend-build
WORKDIR /app/src/frontend

COPY src/frontend/package.json src/frontend/package-lock.json ./
RUN npm ci

COPY src/frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /src

COPY NuGet.config Pecualia.sln ./
COPY src/backend/Pecualia.Api/Pecualia.Api.csproj src/backend/Pecualia.Api/
RUN dotnet restore src/backend/Pecualia.Api/Pecualia.Api.csproj

COPY src/backend/Pecualia.Api/ src/backend/Pecualia.Api/
RUN dotnet publish src/backend/Pecualia.Api/Pecualia.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000

COPY --from=backend-build /app/publish ./
COPY --from=frontend-build /app/src/frontend/dist ./wwwroot
COPY db ./db

ENTRYPOINT ["dotnet", "Pecualia.Api.dll"]
