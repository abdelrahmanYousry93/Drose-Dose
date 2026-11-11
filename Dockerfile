# Production image: Angular site + ASP.NET Core API in one container (one origin, port 8080).
# Build:  docker build -t dose .
# Run:    docker run -p 8080:8080 -e ConnectionStrings__Dose="..." -e Public__AppBaseUrl="https://your-domain" dose

# ---------- 1. Website (Angular) ----------
FROM public.ecr.aws/docker/library/node:22-bookworm-slim AS web
WORKDIR /web
COPY src/Dose.Web/package.json src/Dose.Web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY src/Dose.Web/ ./
RUN npx ng build --configuration production

# ---------- 2. API (.NET 9) ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS api
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props Dose.slnx ./
COPY src/Dose.Api/Dose.Api.csproj src/Dose.Api/
COPY src/Dose.Application/Dose.Application.csproj src/Dose.Application/
COPY src/Dose.Contracts/Dose.Contracts.csproj src/Dose.Contracts/
COPY src/Dose.Domain/Dose.Domain.csproj src/Dose.Domain/
COPY src/Dose.Infrastructure/Dose.Infrastructure.csproj src/Dose.Infrastructure/
RUN dotnet restore src/Dose.Api/Dose.Api.csproj
COPY src/ src/
RUN dotnet publish src/Dose.Api/Dose.Api.csproj -c Release -o /app --no-restore

# ---------- 3. Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /web/dist/dose-web/browser ./wwwroot
# Approved social posts waiting for their time (written by the marketing agency, see marketing/agency/weekly-run.md).
COPY marketing/social-queue.json ./marketing/queue.json
# Uploaded files (payment screenshots, teacher photos). NOTE: container disk is not permanent on most PaaS hosts —
# mount a volume here or move to object storage before real use.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Storage__LocalRoot=/app/private-uploads \
    DOTNET_RUNNING_IN_CONTAINER=true
RUN mkdir -p /app/private-uploads && chown -R app:app /app/private-uploads
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Dose.Api.dll"]
