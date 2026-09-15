# Multi-stage build for the .NET 8 Gasto Buster web app.
# Project lives in the StudentExpenseTracker/ subfolder.
# Build:       docker build -t gastobuster .
# Run:         docker run -p 8080:80 gastobuster

# --- Base: runtime image ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 80

# --- Build: restore + compile ---
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Restore the project first so NuGet layers are cached.
COPY ["StudentExpenseTracker/StudentExpenseTracker.csproj", "StudentExpenseTracker/"]
RUN dotnet restore "StudentExpenseTracker/StudentExpenseTracker.csproj"

COPY . .
WORKDIR "/src/StudentExpenseTracker"
RUN dotnet build "StudentExpenseTracker.csproj" -c Release -o /app/build --no-restore

# --- Publish ---
FROM build AS publish
RUN dotnet publish "StudentExpenseTracker.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# --- Final: runtime image ---
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:80
ENTRYPOINT ["dotnet", "StudentExpenseTracker.dll"]