# Intelligent Sales Assistant - Portfolio Edition

[Svenska](README.md) | [English](README.en.md) | [Enkel version](README.simple.md) | [Portfolio](README.portfolio.md)

This project is a high-performance, secure, and cost-optimized microservices platform built using **.NET 9** and **C# 12**. It is designed to automate B2B sales workflows by integrating real-time Swedish company registry data with Google's Gemini AI to dynamically generate customized websites and sales/marketing content.

---

## 🚀 Key Architectural Concepts & Technologies

### 1. Microservices Architecture
The system consists of two independent services communicating over lightweight HTTP APIs:
* **Service A (Core API - IntelligentSalesAssistantAPI)**: Orchestrates business workflows, manages users/roles via JWT, interacts with SQLite via EF Core, and consumes Service B.
* **Service B (AI Content Engine - ContentEngine)**: Acts as a dedicated proxy for Google's Gemini LLM API, ensuring isolation of heavy AI workloads.

### 2. High-Performance Service-to-Service Communication
* **Lean Payload Architecture**: Instead of passing heavy generated HTML over the network, Service B returns highly optimized JSON schemas (3-5 KB). HTML construction is handled locally by Service A using pre-loaded templates. This reduces network overhead by **90-95%**.

### 3. Production Security & Hardening
* **Zero Hardcoded Secrets**: Leverages `.NET User Secrets` locally and Azure Key Vault in production.
* **Service-to-Service Authentication**: Handshake between Service A and Service B is secured programmatically using a custom `DelegatingHandler` (`ApiKeyHandler`) which appends an API key header (`X-Api-Key`) to outgoing requests.
* **No Secret Logging**: The API handlers and middleware are programmatically verified to prevent logging sensitive headers (e.g. `Authorization`, `X-Api-Key`) or raw request/response bodies.
* **Robust Resilience**: Built with `Polly` inside `Microsoft.Extensions.Http.Resilience` implementing Retry, Circuit Breaker, and Attempt Timeout.
* **Rate Limiting**: Protects public endpoints from abuse using fixed-window rate limiting.

### 4. Serverless Cloud Deployment & "Scale to Zero"
* **Azure Container Apps (ACA)**: Both microservices are deployed inside Azure Container Apps in a shared environment (`env-joco-inventory`).
* **Scale to Zero**: To eliminate idle hosting costs, both container apps are configured with `min-replicas: 0` and `max-replicas: 3`. The containers automatically shut down when inactive and dynamically cold-start when requests arrive, ensuring **100% cost-efficiency**.
* **Managed Identities**: Clean and secure connection between Azure Container Apps and Azure Container Registry (ACR) using Managed Identities (`acrpull` role) rather than static admin credentials.

---

## 🛠️ Technology Stack
* **Runtime**: .NET 9.0 (C# 12)
* **Framework**: ASP.NET Core Web API (Minimal APIs & Controllers)
* **Database**: Entity Framework Core & SQLite
* **API Specs**: Scalar (OpenAPI 3.0)
* **Resilience**: Polly
* **AI Provider**: Google Gemini API via typed HttpClient

---

## 👤 Author & Contact

**Joco Borghol**
* **LinkedIn**: [linkedin.com/in/joco-borghol-777b59386](https://www.linkedin.com/in/joco-borghol-777b59386)
* **GitHub**: [@JocoBorghol](https://github.com/JocoBorghol)
