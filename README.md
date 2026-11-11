# Template project for Blazor WebAssembly on AWS S3

English | [日本語](README.ja.md)

[Template](https://usausa.github.io/template/) project for Blazor WebAssembly on AWS S3.

# ✅ Implement

| Category | Feature |
| --- | --- |
| Auth | Cognito Managed Login (OIDC code + PKCE) / Identity Pool (per-user S3 access) / API Gateway JWT authorizer / Admin-issued users |
| Data | Per-user files on S3 (`users/{sub}/`) / CSV view (summary, chart, table) |
| API | API Gateway HTTP API / Lambda (Annotations) |
| Hosting | S3 + CloudFront (OAC, security headers, SPA fallback) |
| Infra | AWS CDK (C#) / Setup, update, and removal scripts (PowerShell) |

# 📚 Docs

The documents in `docs` are in Japanese.

| Document | Contents |
| --- | --- |
| [architecture.md](docs/architecture.md) | Design (assumptions, AWS resources, authentication and authorization, performance, security) |
| [setup.md](docs/setup.md) | New project, setup, deployment, removal, program structure, scripts, manual procedures, local development, and extension |
| [notes.md](docs/notes.md) | Implementation pitfalls, running costs, and known limitations |

# 🔗 Related Projects

| Project | Description |
| --- | --- |
| [template-aws-lambda](https://github.com/usausa/template-aws-lambda) | AWS Lambda + API Gateway template |
| [template-aws-api-tenant](https://github.com/usausa/template-aws-api-tenant) | Multi-tenant serverless API template on AWS |
| [template-aws-line-miniapp](https://github.com/usausa/template-aws-line-miniapp) | LINE Mini App (LIFF) template on AWS |
| [template-azure-functions](https://github.com/usausa/template-azure-functions) | Azure Functions (isolated worker) template |

# 📦 My Libraries

| Library | Description |
| --- | --- |
| [Usa.Smart.Mapper](https://github.com/usausa/Smart-Net-Mapper) | Object mapper |
