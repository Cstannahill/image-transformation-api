# Stage 1: Base runtime image (for final)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
# Allow install of OS packages as root
USER root

# Install OS deps required by SkiaSharp
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      libfontconfig1 \
      libharfbuzz0b \
 && rm -rf /var/lib/apt/lists/*

# Ensure Kestrel listens on port 80
ENV ASPNETCORE_URLS=http://+:80

# Switch back to non-root user for security
USER $APP_UID
WORKDIR /app
EXPOSE 80

# Stage 2: Build & restore
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src
# Copy only csproj and restore (leverages Docker cache)
COPY ["ImageApi/ImageApi.csproj", "ImageApi/"]
RUN dotnet restore "ImageApi/ImageApi.csproj"

# Copy everything else & build
COPY . .
WORKDIR "/src/ImageApi"
RUN dotnet build "ImageApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Stage 3: Publish (including native assets for linux-x64)
FROM build AS publish
ARG BUILD_CONFIGURATION=Release

RUN dotnet publish "ImageApi.csproj" \
    -c $BUILD_CONFIGURATION \
    -r linux-x64 \
    --self-contained false \
    -o /app/publish \
    /p:UseAppHost=false

# Stage 4: Final image
FROM base AS final
WORKDIR /app

# Copy published output (with libSkiaSharp.so in runtimes/linux-x64/native)
COPY --from=publish /app/publish ./

ENTRYPOINT ["dotnet", "ImageApi.dll"]
