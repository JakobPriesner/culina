# Security Policy

## Supported Versions

Only the **latest released minor version** (`1.7.x` and `main`) receives security patches. Upgrade to the latest container image (`ghcr.io/jakobpriesner/culina:latest`) before reporting an issue.

## Reporting a Vulnerability

**Do not open public GitHub issues for security vulnerabilities.**

Report vulnerabilities via [**GitHub Private Vulnerability Reporting**](https://github.com/JakobPriesner/culina/security/advisories/new) with:

- Affected version / commit SHA and deployment topology
- Minimal reproduction steps or proof-of-concept request
- Impact assessment (e.g., cross-household data access, SSRF bypass, auth bypass)

**Response targets:** Acknowledgment within **72 hours**, triage within **7 days**, and coordinated fix release within **30 days** for high/critical issues.

## Scope & Threat Model

See [`docs/security-review.md`](docs/security-review.md) for the full architecture and controls.

- **In scope:** Authentication/JWT/refresh-token rotation, WebAuthn passkeys, MCP OAuth 2.1 + PAT scopes, household/cookbook authorization boundaries, SSRF protection on recipe URL/federation/Ollama fetches (`SafeHttpDownloader`), XSS in scraped/markdown content, SQL injection in FTS/smart filters, and AES-256-GCM settings encryption.
- **Out of scope:** Compromised host OS/Docker daemon, physical device access to an unlocked browser/phone, rate-limit tuning on unconfigured reverse proxies, and vulnerabilities in upstream third-party AI APIs.
