# Image Manipulation & CDN Delivery API

A **lightweight**, **serverless-ready** .NET 8 Web API for on‑the‑fly image transformations (resize, crop, format conversion, filters, watermarking, rounded cropping) with built‑in rate limiting, API key quotas, logging, and Prometheus metrics. Easily self‑hosted in Docker, deployed to Azure Functions or Kubernetes, and consumed from C#, TypeScript, or any HTTP client.

---

## 🔖 Features

* **Minimal .NET 8** implementation (top‑level statements, minimal APIs)
* Core operations:

  * `POST /resize` (dimensions, format)
  * `POST /crop` (rectangle)
  * `POST /convert` (format)
  * `POST /filter` (grayscale, invert, blur)
  * `POST /watermark` (text, font size, opacity, margin)
  * `POST /crop/rounded` (rectangle + corner radius)
* **OpenAPI / Swagger** for interactive docs and client generation
* **API key** authentication from `X-Api-Key` header
* **Rate limiting** (per‑minute token bucket) + **daily quotas** (in‑memory by key)
* **Serilog** request/response logging
* **Prometheus** metrics (`/metrics`, HTTP metrics middleware)
* **Health check** endpoint (`GET /health`)
* **Dockerfile** optimized for Alpine‑based ASP.NET runtime + native dependencies

---

## 📦 Prerequisites

* [.NET 8 SDK](https://dotnet.microsoft.com/download)
* [Docker](https://docs.docker.com/get-docker/)
* (Optional) Azure CLI for deployment
* (Optional) GitHub Actions with secrets:

  * `ACR_NAME` – your ACR short name (e.g. `myregistry`)
  * `AZURE_CREDENTIALS` – service principal JSON (`az ad sp create-for-rbac --sdk-auth`)
  * `AZURE_SUBSCRIPTION_ID` – your Azure subscription GUID

---

## 🚀 Getting Started

### 1. Clone & Build

```bash
git clone https://github.com/<your-org>/image-api.git
cd image-api/ImageApi
dotnet restore
dotnet build -c Release
```

### 2. Run Locally

```bash
dotnet run --urls "http://localhost:5106"
```

* Swagger UI: `http://localhost:5106/swagger`
* Health: `http://localhost:5106/health`
* Metrics: `http://localhost:5106/metrics`

### 3. Docker

```bash
docker build -t imageapi:latest .
docker run -p 5000:80 imageapi:latest
```

* API: `http://localhost:5000`
* Swagger: `http://localhost:5000/swagger`

### 4. Azure Container Registry (CI/CD)

Use the provided GitHub Actions workflow (`.github/workflows/ci.yml`) to build and push to your Azure Container Registry.

---

## 🔒 Authentication & Authorization

* All endpoints (except `/health`, `/metrics`) require a valid API key in the `X-Api-Key` header.
* Define your keys and limits in `appsettings.json` under `ApiKeyStore`.

```json
"ApiKeys": {
  "dev-key-123": { "RequestsPerMinute": 60, "DailyLimit": 1000 },
  "prod-key-456": { "RequestsPerMinute": 600, "DailyLimit": 10000 }
}
```

---

## ⚙️ Endpoints

All mutation endpoints accept `multipart/form-data` with `file` + other form fields. Responses are raw image bytes with the correct `Content-Type`.

| Path            | Method | Form Fields                                           | Success Status | Description               |
| --------------- | ------ | ----------------------------------------------------- | -------------- | ------------------------- |
| `/resize`       | POST   | `file`, `width`, `height`, `fmt`                      | 200            | Resize to WxH             |
| `/crop`         | POST   | `file`, `x`, `y`, `width`, `height`, `fmt`            | 200            | Crop rectangle            |
| `/crop/rounded` | POST   | `file`, `x`, `y`, `width`, `height`, `radius`, `fmt`  | 200            | Crop with rounded corners |
| `/convert`      | POST   | `file`, `fmt`                                         | 200            | Change image format       |
| `/filter`       | POST   | `file`, `type`, `intensity?`, `fmt`                   | 200            | Grayscale / Invert / Blur |
| `/watermark`    | POST   | `file`, `text`, `fontSize`, `opacity`, `margin`,`fmt` | 200            | Add text watermark        |
| `/health`       | GET    | none                                                  | 200            | Liveness probe            |
| `/metrics`      | GET    | none                                                  | 200            | Prometheus metrics        |

```bash
# Example: resize
curl -X POST http://localhost:5000/resize \
  -H "X-Api-Key: dev-key-123" \
  -F file=@./input.jpg \
  -F width=200 -F height=200 -F fmt=png \
  --output resized.png
```

---

## 📊 Metrics & Logging

* **Serilog** writes structured logs to `log-*.txt` in `/logs`.
* **Prometheus** middleware exposes:

  * `/metrics` for Prometheus scraping
  * HTTP metrics (request counts, durations, status codes)

---

## 💡 Generating Clients

### TypeScript

```bash
npm install -g @openapitools/openapi-generator-cli
openapi-generator-cli generate \
  -i http://localhost:5000/swagger/v1/swagger.json \
  -g typescript-fetch \
  -o sdk/typescript
```

### .NET

```bash
dotnet tool install --global NSwag.ConsoleCore
nswag openapi2csclient \
  /input:http://localhost:5000/swagger/v1/swagger.json \
  /namespace:<Your.Namespace>.Client \
  /output:sdk/dotnet/ImageApiClient.cs
```

---

## 🤝 Contributing & Next Steps

* **Filters**: add more image filters (sharpen, brightness, contrast)
* **AI enhancements**: plug in AI upscaling or style transfer
* **CDN integration**: integrate Azure CDN or Cloudflare for global delivery
* **Serverless**: deploy to Azure Functions with Docker container image

---

© 2025 Your Company or Name. Licensed under MIT.
