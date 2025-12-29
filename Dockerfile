
# =========================
# BUILD STAGE
# =========================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY ["CjEndpoint/CjEndpoint.csproj", "CjEndpoint/"]
RUN dotnet restore "CjEndpoint/CjEndpoint.csproj"

# Copy the rest of the source and publish
COPY . .
WORKDIR /src/CjEndpoint
RUN dotnet publish "CjEndpoint.csproj" -c Release -o /app/out

# =========================
# RUNTIME STAGE
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

COPY --from=build /app/out .

# Render injects PORT automatically
ENV ASPNETCORE_URLS=http://*:${PORT}

CMD ["dotnet", "CjEndpoint.dll"]
