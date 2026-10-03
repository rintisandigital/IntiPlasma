# Web.App (admin office, MVC) — also runs the background jobs (outbox, attachment cleanup). PLAN-WEBAPP §23.7.
# The stage names are the ones Visual Studio expects (fast mode uses "base").

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
# Shared attachment volume, writable by the non-root app user (a new named volume copies this ownership).
RUN mkdir -p /app/uploads && chown "$APP_UID" /app/uploads
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080 \
    FileStorage__RootPath=/app/uploads
EXPOSE 8080
# No curl in the image: probe the liveness endpoint with bash's /dev/tcp.
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /health/live HTTP/1.0\\r\\nHost: localhost\\r\\n\\r\\n' >&3 && grep -q ' 200 ' <&3"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/Web.App/Web.App.csproj", "src/Web.App/"]
COPY ["src/Infrastructure/Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/Application/Application.csproj", "src/Application/"]
COPY ["src/Domain/Domain.csproj", "src/Domain/"]
COPY ["src/SharedKernel/SharedKernel.csproj", "src/SharedKernel/"]
RUN dotnet restore "./src/Web.App/Web.App.csproj"
COPY . .
WORKDIR "/src/src/Web.App"
RUN dotnet build "./Web.App.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Web.App.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Web.App.dll"]
