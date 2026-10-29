# Security Architecture & Review

Overview of Culina's threat model and implemented security controls across authentication, data isolation, outbound network requests, and storage.

---

## 1. Threat Model & Trust Boundaries

1. **Public Internet / Reverse Proxy → Kestrel (`:8080`):** Untrusted callers hitting `/api/v1/*`, `/mcp`, `/shared/*`, and static assets.
2. **Household Isolation Boundary:** Authenticated users belong to a `HouseholdId`. Users must never read or mutate another household's recipes, meal plans, shopping lists, pantry, or settings unless explicitly granted via `CookbookMember`, `RecipeShare`, or public share token.
3. **Private User Boundary (Within a Household):** Nutrition diary logs (`FoodLogEntry`), body weight (`WeightEntry`), calorie/macro goals (`NutritionGoal`), water intake (`WaterIntakeLog`), personal ratings (`UserRecipeRating`), and favorites (`RecipeFavorite`) are strictly scoped to `UserId` and are never exposed to other members of the same household.
4. **Outbound Network Boundary (`Culina.Api` → External Hosts):** Recipe URL scraping, image downloads, connected library sync, Open Food Facts lookups, and user-configured Ollama endpoints are untrusted SSRF vectors.

---

## 2. Authentication & Session Controls

- **Password Hashing:** ASP.NET Core Identity `PasswordHasher<User>` (PBKDF2-HMAC-SHA512, 100,000+ iterations, 128-bit salt). Minimum length 8 chars, max 256 chars.
- **WebAuthn Passkeys (`Fido2NetLib`):** Phishing-resistant passkey registration and login bound to `WebAuthn:ServerDomain` and `WebAuthn:Origins`, with challenge state stored server-side and single-use signature counter verification.
- **Short-Lived JWT + Rotating Refresh Cookie:**
    - Access tokens expire in `15 min` (`HMAC-SHA256` with `>= 32-byte` `CULINA_JWT_SECRET`) and are held in memory (or native iOS Keychain on Capacitor).
    - Refresh tokens (`culina_rt`) are `32-byte` CSPRNG tokens stored hashed (`SHA-256`) in `RefreshTokens`, sent as `HttpOnly; SameSite=Lax; Path=/api/v1/auth` (`Secure` on HTTPS origins), and rotated on every `/auth/refresh`.
    - Presenting an already-rotated refresh token revokes the entire token chain (`RevokedTokens`).
- **Personal Access Tokens & MCP OAuth 2.1:**
    - PATs (`culina_pat_<random>`) store only their SHA-256 hash and prefix (`TokenPrefix`) in the database.
    - MCP OAuth 2.1 requires PKCE (`S256`), validates redirect URIs against loopback/HTTPS rules, and issues scoped tokens tied to the consenting user.

---

## 3. SSRF & Outbound Request Hardening (`SafeHttpDownloader`)

Every outbound HTTP request triggered by user input (URL recipe import, remote image download, connected library feed sync, Ollama connectivity test) uses `SafeHttpDownloader`:

- **Scheme whitelist:** Only `http://` and `https://` are permitted (`file://`, `ftp://`, `gopher://`, `data:` are rejected).
- **DNS Resolution & IP Pinning:** Resolves hostnames before connecting and rejects every resolved IPv4/IPv6 address in reserved ranges:
    - Loopback (`127.0.0.0/8`, `::1`) — blocked in Production (allowed only for Ollama when `CULINA_ALLOW_LOOPBACK_OLLAMA=true`).
    - Private RFC 1918 (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`) & RFC 6598 CGNAT (`100.64.0.0/10`).
    - Link-local & Cloud Metadata (`169.254.0.0/16`, `fe80::/10`, `fd00::/8`, `0.0.0.0/8`).
- **Redirect Re-validation:** Follows at most `5` redirects, re-validating the target URL and resolved IP on every hop.
- **Size & Time Caps:** Enforces strict response byte limits (`2 MB` for HTML/JSON feeds, `15 MB` for images/archives) and `15s` socket/read timeouts.

---

## 4. Input Validation, XSS, SQLi & File Handling

- **SQL Injection:** All database queries use parameterized EF Core LINQ or parameterized `FormattableString` (`SqlQuery<T>` / `ExecuteSqlAsync`). Search queries (`QueryGrammar`, `SmartFilterEvaluator`) parse into strongly-typed AST nodes and bind parameters (`@p0`, `@p1`) — user text is never interpolated into raw SQL.
- **Uploaded Images (`ImageStorageService`):** Uploaded files are decoded pixel-by-pixel via `SixLabors.ImageSharp` (rejecting non-image polyglots), stripped of EXIF GPS/metadata, resized to max `1600×1600`, re-encoded as `.webp`, and written under random GUID filenames in `/app/uploads`.
- **Archive Import (`ArchiveService`):** Enforces entry count (`<= 2,000`), uncompressed byte cap (`<= 250 MB` zip-bomb guard), and canonical path checks (`Path.GetFullPath`) to block Zip-Slip traversal.
- **Encryption at Rest (`SettingsEncryptionService`):** Household AI API keys (`app_settings.*_encrypted`) and WebPush VAPID private keys are encrypted with `AES-256-GCM` (random 96-bit nonce + 128-bit authentication tag per value) using the 256-bit key derived from `CULINA_SETTINGS_ENCRYPTION_KEY`.

---

## 5. HTTP Security Headers & Container Hardening

- **Response Headers (`SecurityHeadersMiddleware`):**
    - `Content-Security-Policy`: `default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob: https:; connect-src 'self' https:; frame-ancestors 'none'; base-uri 'self'`
    - `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy: camera=(self), microphone=(), geolocation=()`.
- **No Source Maps in Production:** `vite.config.ts` sets `build.sourcemap: false` and `SecurityHeadersMiddleware` returns `404` for any `.map` path.
- **Non-Root Runtime:** `culina-api` runs under unprivileged user `culina` (`UID 10001`), while PostgreSQL runs under `postgres` listening only on `127.0.0.1:5432`.
