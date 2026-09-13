# Security review — OWASP ASVS 5.0, Level 2

A record of what was checked, what the evidence is, and what is deliberately
not there. **This is not a certification and does not claim one.** It is one
maintainer's review of one application against a published checklist, so that
the next person can see what was looked at and what was not.

| | |
| --- | --- |
| Reviewed | 13 September 2026, against ASVS 5.0 Level 2 |
| Scope | The application, the container image, and the deployment `compose.prod.yaml` describes |
| Owner | The maintainer |
| Method | Source review, plus tests that fail when the property stops holding |

Everything in the Evidence column is an executable test unless it says
otherwise. A property asserted only by a document is a property nobody will
notice losing.

## V2 — Authentication

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| Passwords stored with a modern KDF | Met | Argon2id, cost from configuration, rehashed on sign-in when the cost rises. `PasswordHashingSettings`, `Register_ShouldRejectAShortPassword_AtTheBoundary` |
| No credential in a response, log or URL | Met | `Register_ShouldNeverEchoThePassword_InAnyForm`, `SignIn_ShouldNeverPutTheSessionTokenInTheBody_OnlyInAnHttpOnlyCookie`, `RequestLogging_ShouldRecordNoBody_InAnyEnvironment` |
| Sign-in does not disclose whether an account exists | Met | `SignIn_ShouldAnswerIdentically_WhetherOrNotTheAccountExists`, and `SignIn_ShouldCostTheSame_WhetherOrNotTheAccountExists` for the timing channel |
| Brute force is limited | Met | Per address **and** per account: either alone leaves an attack open. `Registration_ShouldBeRefused_OnceTheHourlyLimitIsReached`, `RateLimitSettings` |
| Rate-limit responses say when to retry | Met | `Rejection_ShouldSayWhenToRetry_SoAClientCanBackOffCorrectly` |
| Account recovery does not disclose account existence | **Not applicable — no recovery exists** | There is no password reset and no "forgot password". See Exceptions. |

## V3 — Session management

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| Session identifier is opaque and server-side | Met | The cookie carries a reference; the row authenticates. `FindActiveByToken_ShouldReturnTheSession_WhenTheCookieValueIsCorrect` |
| Cookie is `HttpOnly`, `SameSite`, `Secure`, host-prefixed | Met | `__Host-culina.session`. `Secure` is configurable only because a browser refuses a `__Host-` cookie over plain HTTP; production sets it true |
| A new session is issued per sign-in | Met | No session exists before authentication, so there is nothing to fixate |
| Sessions can be listed and revoked | Met | `Sessions_ShouldListTheCallersDevices_AndMarkTheCurrentOne` |
| Revocation takes effect immediately | Met | `Session_ShouldStopWorking_TheMomentItIsRevokedFromAnotherDevice` |
| One account cannot revoke another's session | Met | `Revoke_ShouldNotSeeAnotherUsersSession_EvenWithItsExactId` |
| Sessions expire | Met | `Cookies__SessionDays`, `DeleteExpired_ShouldRemoveLapsedSessions_ButKeepLiveOnes` |
| Sign-out ends the session server-side | Met | `SignOut_ShouldEndTheSession_SoTheCookieStopsWorking` |

## V4 — Access control

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| Enforced server-side on every request | Met | Membership is read per request, never cached in the session |
| Horizontal isolation, tested with real identifiers | Met | `Stranger_ShouldSeeNothingOfAnotherHousehold_EvenWithItsExactIds` and `…ShouldChangeNothing…` cover read, list, search, image, shopping list, cook session and write across a household boundary |
| Failures do not disclose existence | Met | 404 rather than 403 throughout; `Delete_ShouldAnswerTheSame_ForAStrangerAndForNothingAtAll` covers the idempotent case where the status differs from the rest |
| Revoked membership takes effect immediately | Met | `Member_ShouldLoseAccess_TheMomentTheyAreRemoved` |
| Role checks on privileged operations | Met | `Rename_ShouldBeRefusedForAPlainMember_EvenThoughTheyCanSeeIt`, `Delete_ShouldBeRefusedForAPlainMember`, `Settings_ShouldBeForbidden_ForAnAccountThatIsNotTheAdministrator` |
| The last owner cannot strand a household | Met | `RemoveMember_ShouldRefuseToStrandTheHousehold_WhenItIsTheLastOwner` |
| Personal notes are not shared by membership | Met | `Notes_ShouldBeInvisibleToAnotherMemberOfTheSameHousehold`, `CookLog_ShouldBeSeparatePerPerson_InTheSameHousehold` |

## V4 — Invitations

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| The code is stored hashed | Met | `code_hash`; the plaintext is returned once and never again — `Invite_ShouldReturnTheCodeOnce_AndNeverAgain` |
| Single use | Met | `Redeem_ShouldWorkOnlyOnce_SoACodeCannotBeShared` |
| Expires | Met | `IsUsable_ShouldBeFalse_OnceItHasExpired`, and end to end in `Redeem_ShouldRefuseACodeThatHasExpired` |
| Revocable | Met | `Revoke_ShouldStopACodeWorking_Immediately` |
| Refusals are indistinguishable | Met | `Redeem_ShouldAnswerIdentically_ForUnknownAndUsedCodes` |
| Only a member may invite | Met | `Invite_ShouldBeRefused_ForSomeoneWhoIsNotAMember` |

## V5 — Validation and encoding

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| Every input is validated server-side | Met | Value objects in the domain; a request that cannot be read is a 400 problem document, not a 500 — `Update_ShouldBlameTheCaller_WhenTheBodyCannotBeRead` |
| Unknown parameters are refused, not ignored | Met | `Request_ShouldBeRejected_WhenItCarriesAnUndeclaredParameter`. A mistyped filter that is silently dropped returns everything while the caller believes it filtered |
| SQL is parameterised | Met | Hand-written SQL through Dapper with parameters throughout; no string concatenation of user input |
| Output encoding | Met | Svelte escapes by default; no `{@html}` anywhere in the app |

## V12 — Files and resources

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| The bytes decide the type, not the header | Met | Decoding is the check. `Upload_ShouldRejectAFileThatLiesAboutWhatItIs` |
| Uploads are re-encoded, never served back as received | Met | Always WebP, always Culina's own encoding. `Upload_ShouldAttachTheImage_AndServeItAsWebp` |
| A byte limit is enforced while reading | Met | Bounded copy; the stream is never read past the limit |
| A pixel limit is enforced before decoding | Met | The header is read first: a byte limit does not bound a pixel count. `Upload_ShouldRefuseAnImageTooLargeToDecode_WithoutDecodingIt` |
| Metadata is removed | Met | `Served_ShouldCarryNoMetadataFromTheOriginal`. A photograph taken in a kitchen carries where that kitchen is |
| Files are not served from a caller-controlled path | Met | The stored name is a content hash and a width from a fixed list. `Served_ShouldRefuseAWidthItDoesNotKeep` |
| Images are private | Met | `Cache-Control: private`, and the access check runs before the file is opened. `Served_ShouldCarryAContentHashETag_AndBePrivate`, `Upload_ShouldBeRefused_ForARecipeInAnotherHousehold` |

## V13 — Configuration and headers

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| Security headers on every response | Met | `EveryResponse_ShouldCarryTheSecurityHeader_WhenHandled` |
| CSP with no `unsafe-inline` | Met | A per-response nonce; the one inline script carries it |
| CSRF defence on every unsafe request | Met | Token **and** origin, checked in that order. `UnsafeRequest_ShouldBeRejected_WhenTheHeaderIsMissing`, `UnsafeRequest_ShouldBeRejected_WhenTheTokenBelongsToAnotherSession`, `Request_ShouldBeRejected_WhenCookiesAreNotSecureAndTheOriginIsForeign` |
| Exactly one endpoint is exempt from CSRF, deliberately | Met | `ExemptEndpoint_ShouldStillBeTheOnlyOneExempted`. Signing in has no session to carry a token, and without the exemption a lost CSRF cookie makes even signing out impossible |
| Forwarded headers trusted only from named proxies | Met | `ForwardedHeadersSettings`, validated at startup; trusting everything would let any client forge its address |
| The process refuses to start on invalid configuration | Met | `SettingsValidationTests` |
| No secret in any committed file | Met | Gitleaks over full history, on every change |
| Dependencies scanned | Met | `NuGetAudit` at `low` fails the compile; `pnpm audit`, Trivy on the image, and a nightly rescan of the published image |
| The runtime has no shell or package manager | Met | Chiseled base, non-root, read-only filesystem, all capabilities dropped |

## V7 — Logging

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| No credential or personal content in logs | Met | `RequestLogging_ShouldRecordNoBody_InAnyEnvironment` — no bodies, no headers, no query strings |
| Security-relevant events are logged | Met | Foreign-origin and CSRF rejections are logged with the request id, at a level an operator can alert on |
| Every response carries a correlation id | Met | `EveryResponse_ShouldCarryARequestId_WhenTheRequestIsHandled` |
| An exception is logged once, and never reaches the client | Met | `GlobalExceptionHandler` is the only place; the message is never in the response |

## Offline, and what it cannot promise

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| Authenticated responses are not cached generically | Met | An allow-list of exact paths, and a test that nothing else can appear in the store: `keeps nothing from the API that anyone did not ask it to` |
| Private material is cleared when the session changes | Met | On sign-in as well as sign-out. `is gone from the device the moment anyone signs out`, `keeps nothing of the first person for the second` |
| Cached responses never override the server while online | Met | Network-first for everything but the immutable image; the cache only catches a fall |
| The store is bounded | Met | 120 entries, oldest evicted |
| A reload is never imposed while somebody is working | Met | `says nothing while somebody is cooking or editing`, and it is offered again at the next safe moment |
| Revocation on a disconnected device | **Cannot be met** | A device that is not in contact with the server cannot learn anything from it. Documented plainly in `docs/operations.md` rather than papered over |

## Exceptions, with reasons

**No account recovery.** There is no "forgot password" flow, and therefore no
recovery oracle to get wrong. An email-based reset needs an SMTP configuration
that a self-hoster would have to maintain, and a self-hosted instance's
administrator can reset a password directly. This is a deliberate absence, not
an oversight — but it does mean that on an instance with exactly one account,
losing the password means losing the instance. Worth revisiting before there
are many instances.

**No multi-factor authentication.** ASVS Level 2 expects it. Culina is a recipe
app for a household, its sessions are opaque and revocable, and sign-in is rate
limited both ways. The cost of a second factor for this audience is higher than
what it buys, and adding it badly would be worse than not adding it. Recorded
here rather than quietly skipped.

**No per-request audit trail.** Who changed a recipe is not recorded beyond the
usual logs. For a shared kitchen this is a feature, not a gap.

**Content Security Policy allows `img-src data: blob:`.** The editor previews a
photograph before it is uploaded, which needs one of them. It is narrower than
it looks: there is no `unsafe-inline`, no `unsafe-eval`, and scripts are
`'self'` plus a nonce.

## What this review did not cover

The reverse proxy, the host, the database server, and everything the operator
runs alongside them. TLS configuration, in particular, is entirely theirs —
`docs/operations.md` says what the app expects and nothing more.

## Importing a recipe from a URL

`POST /api/v1/recipe-imports` is the one endpoint that makes the **server** open
a connection to an address a user chose, from inside whatever network it is
deployed in. Unguarded, that is a server-side request forgery hole: read the
cloud metadata service for credentials, reach the database on the next
container, or use reply timing as a port scanner.

Five controls, in `SafeWebPageFetcher`, and all five are load-bearing:

1. **Scheme allowlist.** `http` and `https` only. `file:` reads the disk;
   `gopher:` writes arbitrary bytes to an arbitrary port.
2. **Every connection goes to a checked address.** The host is resolved inside
   the socket's connect callback, every returned address is checked against
   `PublicAddress`, and the socket dials *the address that was checked*.
   Validating a name and then handing the name to the socket leaves the window
   that DNS rebinding lives in.
3. **Redirects are followed by hand**, five at most, each hop validated again
   from scratch. Automatic redirects would take the second hop with none of this.
4. **A deadline** on the whole exchange, so a server answering one byte a minute
   cannot hold a connection.
5. **A cap on what is read**, enforced while reading rather than after: a
   `Content-Length` is a claim, not a limit.

`PublicAddress` denies loopback, private, carrier-grade NAT, link-local
(including `169.254.169.254`), benchmarking, documentation, multicast and
broadcast ranges, their IPv6 equivalents, unique-local, the NAT64 well-known
prefix, and **IPv4 addresses wrapped in IPv6** — checking the wrapper instead of
the value is exactly how `::ffff:127.0.0.1` gets through.

**Refusals are deliberately vague.** Every one reports "that address cannot be
fetched"; distinguishing "blocked" from "timed out" would turn the endpoint into
a port scanner with a friendly error message. The host is logged for the
operator and never returned to the caller.

The endpoint requires a session and has its own hourly rate limit
(`RateLimits__ImportsPerHour`, default 30), separate from everything else.

`PublicAddressTests` covers every range; `SafeWebPageFetcherTests` starts a real
HTTP server on loopback and asserts the fetcher never connects to it.

**It is an import, not a scraper.** One page at a time, at a person's request,
for their own use. The fetcher identifies itself plainly rather than
impersonating a browser, and it reads the structured data a site publishes for
search engines rather than reverse-engineering its markup.
