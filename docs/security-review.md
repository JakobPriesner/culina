# Security review — OWASP ASVS 5.0, Level 2

A record of what was checked, what the evidence is, and what is deliberately
not there. **This is not a certification and does not claim one.** It is one
maintainer's review of one application against a published checklist, so that
the next person can see what was looked at and what was not.

| | |
| --- | --- |
| Reviewed | 13 September 2026, against ASVS 5.0 Level 2; a full-code follow-up on 1 October 2026 (below) |
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
| Brute force is limited | Met | Per address **and** per account: either alone leaves an attack open. The anonymous limits and the global one count per address whatever cookie comes with the request, since the limiter runs before the cookie is checked and a made-up one would otherwise buy a fresh budget. `Registration_ShouldBeRefused_OnceTheHourlyLimitIsReached`, `SignIn_ShouldBeRefused_WhenEachAttemptCarriesADifferentMadeUpSessionCookie`, `RateLimitSettings` |
| Rate-limit responses say when to retry | Met | `Rejection_ShouldSayWhenToRetry_SoAClientCanBackOffCorrectly` |
| Account recovery does not disclose account existence | Met | One answer, `400 auth.invalid_recovery_code`, for an unknown address, a wrong, used, expired or foreign code; redemption is one statement whichever it is. `Reset_ShouldAnswerIdentically_ForAnUnknownAddressAndAWrongCode` |
| Password change needs the current password | Met | `ChangePassword_ShouldBeRefused_WhenTheCurrentPasswordIsWrong`; making recovery codes needs it too, `RecoveryCodes_ShouldNeedThePassword_SoAStolenSessionCannotKeepTheAccount` |

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
| Expiry is idle time, with an absolute ceiling | Met | Renewed on use, at most once per `Cookies__RenewAfterHours`, but never past `Cookies__MaxSessionDays` (90 by default) from sign-in, so a stolen cookie cannot renew forever. A renewal cannot revive a session revoked after it was read. `AnAuthenticatedRequest_ShouldReissueBothCookies_OnceTheSessionIsDueForRenewal`, `Renew_ShouldLeaveTheSessionRevoked_WhenItWasRevokedAfterBeingRead`, `CookieValidate_ShouldThrow_WhenTheCeilingIsShorterThanTheIdleLifetime` |
| Sign-out ends the session server-side | Met | `SignOut_ShouldEndTheSession_SoTheCookieStopsWorking` |
| Changing or resetting a password ends other sessions | Met | Change keeps the caller's session and ends the rest, `ChangePassword_ShouldSignOutEveryOtherDevice_ButKeepThisOne`; a reset ends all of them, `Reset_ShouldSetANewPassword_AndSignOutEverySession` |

## V4 — Access control

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| Enforced server-side on every request | Met | Membership is read per request, never cached in the session |
| Horizontal isolation, tested with real identifiers | Met | `Stranger_ShouldSeeNothingOfAnotherHousehold_EvenWithItsExactIds` and `…ShouldChangeNothing…` cover read, list, search, image, shopping list, cook session and write across a household boundary |
| Failures do not disclose existence | Met | 404 rather than 403 throughout; `Delete_ShouldAnswerTheSame_ForAStrangerAndForNothingAtAll` covers the idempotent case where the status differs from the rest |
| Revoked membership takes effect immediately | Met | `Member_ShouldLoseAccess_TheMomentTheyAreRemoved` |
| Role checks on privileged operations | Met | `Rename_ShouldBeRefusedForAPlainMember_EvenThoughTheyCanSeeIt`, `Delete_ShouldBeRefusedForAPlainMember`, `Settings_ShouldBeForbidden_ForAnAccountThatIsNotTheAdministrator` |
| The last owner cannot strand a household | Met | `RemoveMember_ShouldRefuseToStrandTheHousehold_WhenItIsTheLastOwner` |
| Personal notes are not shared by membership | Met | `Notes_ShouldBeInvisibleToAnotherMemberOfTheSameHousehold`, `CookLog_ShouldBeSeparatePerPerson_InTheSameHousehold`; another member saving the recipe cannot erase them either, `Notes_ShouldSurviveAnotherMemberSavingTheRecipe_WhenTheStepIsKept` |

## V4 — Invitations

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| The code is stored hashed | Met | `code_hash`; the plaintext is returned once and never again — `Invite_ShouldReturnTheCodeOnce_AndNeverAgain` |
| Single use | Met | `Redeem_ShouldWorkOnlyOnce_SoACodeCannotBeShared` |
| Expires | Met | `IsUsable_ShouldBeFalse_OnceItHasExpired`, and end to end in `Redeem_ShouldRefuseACodeThatHasExpired` |
| Revocable | Met | `Revoke_ShouldStopACodeWorking_Immediately` |
| Refusals are indistinguishable | Met | `Redeem_ShouldAnswerIdentically_ForUnknownAndUsedCodes` |
| Only a member may invite | Met | `Invite_ShouldBeRefused_ForSomeoneWhoIsNotAMember` |
| Opening a link does not join | Met | The join page redeems only when the person signed in presses Join, so a link from a stranger cannot switch them into a stranger's kitchen — `asks somebody signed in before joining, and does not join on opening` in `join/[code]/page.spec.ts` |

## V2 — Account recovery

Recovery works without mail: something a person holds rather than something
sent to them. Where the administrator has set up mail (Settings → Mail), a
third kind of code can also be mailed. The flow is custom, which is why it is
recorded here.

* **Saved codes.** Ten per account, made on the Password settings page after
  re-entering the password. 80 random bits each (Crockford base 32, four
  groups of four), so they can be copied by hand. They do not expire: they are
  for the day the password is gone. Making a new set ends the old one.
* **Issued codes.** The administrator makes one for an address on the Server
  settings page and passes it on themselves. Same format, valid 24 hours,
  because it has passed through somebody else's hands. This is the way back for
  anyone without saved codes, except the administrator on an instance with no
  other administrator — whose only way back is their own saved codes.
* **Mailed codes.** Only with mail on. `POST /password-reset-links` mails a
  code of the same format, valid one hour, inside a link to the reset page. It
  is stored like the others (`issued_by` null, `expires_at` set), is not one of
  the saved set, and using it also confirms the address.
* **Redeeming.** `POST /password-resets` with the address, the code and a new
  password. The code is used up and every session of the account is revoked;
  the web app then signs in with the new password.

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| Codes stored hashed | Met | `recovery_codes.code_hash`, SHA-256 of the normalised code; the plaintext is in one response only. `RecoveryCodes_ShouldBeShownOnce_AndOnlyCountedAfterwards` |
| Single use, even under a race | Met | Redeeming is one `update … where used_at is null returning`. `Reset_ShouldUseUpTheCode_SoItWorksOnlyOnce` |
| Issued codes expire | Met | `IssuedCode_ShouldBeRefused_OnceItHasExpired` |
| A code unlocks only its own account | Met | The address is part of the same statement. `Reset_ShouldRefuseAnotherAccountsCode_EvenThoughTheCodeIsReal` |
| Brute force is limited | Met | Per address by the sign-in limiter, and per account by the same budget sign-in and password confirmation draw on. `Reset_ShouldRefuseEvenTheRightCode_OnceTheAccountHasFailedTooOften`. Argon2 runs only after a code is known to be good, so guessing costs the server a lookup |
| Only the administrator issues codes | Met | `IssuedCode_ShouldBeForbidden_ForAnAccountThatIsNotTheAdministrator` |
| Failures are counted | Met | `culina.auth.recovery_failures` |
| Asking for a mailed code reveals nothing | Met | `202` whether or not the address is registered, `409 mail.off` the same for every address; the mail is queued, not sent, so the answer takes no longer for a real account. `Links_ShouldBeAccepted_ForAnAddressNobodyHas_AndMailNothing` |
| Nobody can fill a stranger's inbox | Met | Per address by the sign-in limiter, and at most one mail of a kind per account a minute however many clients ask (`MailThrottle`) |

## V2 — Mailed links and address confirmation

With mail on, a sign-in link and an address confirmation are tokens mailed to
the account's address (`email_tokens`). 256 random bits, stored as SHA-256,
single use, 15 minutes for a sign-in link and 48 hours for a confirmation. They
are redeemed by a `POST` from the page, never by opening the URL, so a mail
scanner that fetches every link uses nothing up, and the token travels after
`#`, which no server, proxy or `Referer` sees; the page removes it from the
address bar once read. Links point only at `Site__Url`, never at the request's
`Host`, so a forged host header cannot turn a reset mail into a phishing link.

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| Single use, even under a race | Met | One `update … where used_at is null returning`. `SignInLink_ShouldSignIn_AndWorkOnlyOnce`, `ConfirmationLink_ShouldWorkOnlyOnce` |
| A token does only what it was sent for | Met | The purpose is part of the statement. `ConfirmationToken_ShouldNotSignInAsASignInLink` |
| Refusals are indistinguishable | Met | One answer, `400 auth.invalid_link`, for unknown, used, expired and foreign tokens |
| An unconfirmed address is said only to the password's owner | Met | `auth.email_unconfirmed` comes after the password matched; a wrong password is still `auth.invalid_credentials`. `WrongPassword_ShouldNotSayTheAddressIsUnconfirmed` |
| The mail password and queued mail are unreadable from a dump | Met | Both encrypted with the key ring under their own purposes; the password is never in a response (`ThePassword_ShouldNeverAppearInAnyResponse_OnlyThatThereIsOne`) and only sent to the server it was saved for (`Update_ShouldAskForThePasswordAgain_WhenTheServerChanges`) |
| The owner hears about changes to the way in | Met | Notices for a new kind of device, a changed password, new recovery codes and an issued code. `NewDevice_ShouldBeMailed_OnlyTheFirstTimeItSignsIn`, `PasswordResetLink_ShouldMailACode_ThatSetsANewPassword_AndSayItChanged` |

## V5 — Validation and encoding

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| Every input is validated server-side | Met | Value objects in the domain; a request that cannot be read is a 400 problem document, not a 500 — `Update_ShouldBlameTheCaller_WhenTheBodyCannotBeRead` |
| Unknown parameters are refused, not ignored | Met | `Request_ShouldBeRejected_WhenItCarriesAnUndeclaredParameter`. A mistyped filter that is silently dropped returns everything while the caller believes it filtered |
| SQL is parameterised | Met | Hand-written SQL through Dapper with parameters throughout; no string concatenation of user input |
| `LIKE` patterns carry no wildcard from the caller | Met | Every pattern is built from a fold (`culina_fold_ae`, `culina_fold_a`, `culina_search_terms`, `culina_wanted_words`, `SearchText.FoldAe`), which keeps only `a-z`, `0-9` and spaces, so `%`, `_` and `\` never reach a pattern. A search or an ingredient chip of only `%` or `_` folds to nothing and matches nothing (or, as free text, behaves like an empty box), and a smart-shelf ingredient rule that folds to nothing is refused (`cookbooks.invalid_rule`), since a wordless rule would hold every recipe. `AnIngredientChip_ShouldHoldNothing_WhenItIsOnlyAWildcard`, `ItShouldRefuseAnIngredientRuleThatIsOnlyAWildcard`, the wildcard cases in `RecipeSearchRelevanceTests`. A new `LIKE` over raw input needs `escape` or a fold first |
| Output encoding | Met | Svelte escapes by default; no `{@html}` anywhere in the app |

## V12 — Files and resources

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| The bytes decide the type, not the header | Met | Decoding is the check. `Upload_ShouldRejectAFileThatLiesAboutWhatItIs` |
| Uploads are re-encoded, never served back as received | Met | Always WebP, always Culina's own encoding. `Upload_ShouldAttachTheImage_AndServeItAsWebp` |
| A byte limit is enforced while reading | Met | Bounded copy; the stream is never read past the limit |
| A pixel limit is enforced before decoding | Met | The header is read first: a byte limit does not bound a pixel count. `Upload_ShouldRefuseAnImageTooLargeToDecode_WithoutDecodingIt` |
| An animated image decodes one frame | Met | The pixel limit is one frame's, and each frame of a GIF, WebP, APNG or TIFF is a full canvas: only the first is ever decoded. `Upload_ShouldKeepOnlyTheFirstFrame_OfAnAnimatedImage` |
| Metadata is removed | Met | `Served_ShouldCarryNoMetadataFromTheOriginal`. A photograph taken in a kitchen carries where that kitchen is |
| Only the accepted formats are decoded | Met | The store reads the format from the bytes and decodes only JPEG, PNG, WebP and GIF, not every format Skia knows, so a TIFF, BMP or ICO is refused as unreadable before any decoder runs. `Upload_ShouldRefuseATiff_BecauseOnlyTheAcceptedFormatsAreDecoded` |
| An archive restore is bounded and reads a malformed archive without failing halfway | Met | 64 MB per upload, 2,000 recipes per restore, and the per-person archive budget. Each recipe is its own transaction so one odd recipe cannot block the rest; a step pointing outside its recipe's ingredients keeps its text instead of throwing after earlier recipes were written. `Restore_ShouldRefuse_AnArchiveWithMoreRecipesThanOneRestoreWrites`, `ArchiveRestore_ShouldShareTheArchiveBudget_WithTakingOne`, `Restore_ShouldKeepTheText_WhenAStepPointsAtANegativeIngredientPosition` |
| Files are not served from a caller-controlled path | Met | The stored name is a content hash and a width from a fixed list. `Served_ShouldRefuseAWidthItDoesNotKeep` |
| Images are private | Met | `Cache-Control: private`, and the access check runs before the file is opened. `Served_ShouldCarryAContentHashETag_AndBePrivate`, `Upload_ShouldBeRefused_ForARecipeInAnotherHousehold` |

## V13 — Configuration and headers

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| Security headers on every response | Met | `EveryResponse_ShouldCarryTheSecurityHeader_WhenHandled`. Hashed scripts and stylesheets under `/_app/immutable/` carry only nosniff, CORP and Referrer-Policy: CSP, Permissions-Policy, X-Frame-Options and COOP govern documents and workers, not script or style subresources. Anything else there (an SVG can open as a document), the shell, the service worker and the API keep the full set. `HashedAsset_ShouldKeepNosniffAndTheResourcePolicy_WithoutTheDocumentHeaders`, `EveryOtherResponse_ShouldCarryTheFullSet` |
| CSP with no `unsafe-inline` | Met | A nonce per rendered document; the inline scripts (theme and boot skeleton, route preloads) carry it. The document is `no-store`, but an installed app's service worker precaches the rendered shell together with the CSP header it came with and serves it for every navigation until the next deploy, so on such a device the nonce is fixed per build rather than per response. That is deliberate (header and document always match, and the app opens offline) and harmless while there is no markup sink: no `{@html}`, and no `style` attribute in `app.html` |
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
| Authenticated responses are not cached generically | Met | An allow-list of exact shapes in `service-worker/apiCache.ts`: who is signed in (`/users/me`), the recipe list, each recipe and a household's shopping list (network-first), and recipe images (cache-first, content-addressed). A test checks that nothing else can appear in the store: `keeps nothing from the API that anyone did not ask it to` |
| Private material is cleared when the session changes | Met | On sign-in as well as sign-out, and by the worker itself on the first 401 to one of its reads, for a session that ended while the app was closed. `is gone from the device the moment anyone signs out`, `keeps nothing of the first person for the second`, `forgets the private cache when the server says the session is gone` |
| Cached responses never override the server while online | Met | Network-first for everything but the immutable image; the cache only catches a fall |
| The store is bounded | Met | 120 entries, oldest evicted |
| A reload is never imposed while somebody is working | Met | `says nothing while somebody is cooking or editing`, and it is offered again at the next safe moment |
| Revocation on a disconnected device | **Cannot be met** | A device that is not in contact with the server cannot learn anything from it. Documented plainly in `docs/operations.md` rather than papered over |

## Follow-up review, 1 October 2026

A second, full-code review (backend, frontend, deployment and CI) found fifty
issues, tracked as `culina-v2-kufx` with one child per finding. Every one is
fixed except one that was accepted on purpose (under Exceptions). Where a
finding changed a claim above, the row carries its evidence; the rest:

| Area | What changed | Evidence |
| --- | --- | --- |
| Transactions | A returned failure rolls the unit of work back, so a refused registration writes nothing | tests beside the unit of work |
| Registration | The first account becomes administrator exactly once under a race, and the user limit holds under one; a closed registration is refused before the address is looked at, so it reveals nothing about accounts (an open one still says an address is taken) | `Register_ShouldMakeExactlyOneAdministrator_WhenFirstRegistrationsRace`, `Register_ShouldStopAtTheUserLimit_WhenRegistrationsRaceForTheLastPlace`, `Register_ShouldBeClosed_ForTheSecondAccount_WhenTheAdminHasNotOpenedIt` |
| Sign-in | The lockout counter reserves an attempt atomically, and Argon2 runs at most one hash per core at a time | `TryReserve_ShouldAdmitOnlyTheBudget_WhenGuessesArriveAllAtOnce` |
| Rate limits | Costly limits count per person rather than per session, so signing in again buys no fresh budget | `Import_ShouldBeRefused_WhenThePersonSignsInAgainForAFreshBudget` |
| Images | At most two decodes at once per process, a bounded allocator, and the first frame only | `Upload_ShouldKeepOnlyTheFirstFrame_OfAnAnimatedImage` |
| Outbound fetches | `HTTP(S)_PROXY` is ignored (a proxy would bypass the address check, or is dialled only after it); allowing private sources never allows loopback or the metadata address | `Handler_ShouldRefuseALoopbackTarget_WithoutContactingTheProxy_WhenOneIsConfigured`, `GetAsync_ShouldRefuseLoopback_WhenPrivateAddressesAreAllowed` |
| Secrets at rest | Connected-source tokens are encrypted under their own purpose, and a plaintext token left by an older version is encrypted at start | `AddAsync_ShouldStoreTheTokenEncrypted_AndReadItBack`, `StartAsync_ShouldEncryptATokenStoredBeforeTokensWereEncrypted` |
| Secrets in logs | Share tokens and invitation codes are redacted from the path before it reaches a log or a span | `Redact_ShouldReplaceTheSecretSegment_UnderEveryRouteThatCarriesOne` |
| Configuration | A trusted-proxy network that would trust every client is refused unless the deployment names it; whether cookies may go without `Secure` is the deployment's to say, never the app's settings screen, and running without it logs a warning | `Update_ShouldRefuseAProxyNetworkThatWouldTrustEveryClient_AndSaveNothing`, `Handle_ShouldRefuseAProxyNetworkTooWideToTrust_WithACodeOfItsOwn` |
| Error responses | An unhandled exception still carries every security header and the request id | `UnhandledException_ShouldStillCarryEverySecurityHeaderAndTheRequestId` |
| Households | Removing a member ends the inheritance they set up; a stranger learns nothing about who is in a household | `DeletingAnAccount_ShouldEndTheInheritanceItSetUp`, `Stranger_ShouldLearnNothingAboutWhoIsInAnotherHousehold_OrWhoInheritsIt` |
| Assistant | The monthly budget is reserved under a lock, so concurrent requests cannot overspend it | `ComposeAsync_ShouldReserveAgainstTheConfiguredCeilings` |
| Supply chain | Base images are pinned by digest; the `latest` tag moves only after the CI gates and the image scan | `Dockerfile`, `.github/workflows/release.yml` |
| Deployment | Production compose requires a Postgres superuser password rather than defaulting one; the development database listens on loopback only | `compose.prod.yaml`, `compose.yaml` |

## Exceptions, with reasons

**A fresh instance's setup is anonymous.** Until setup is complete, whoever
reaches the port can point the instance at a database and register first as
administrator. Decided on 24 September 2026 and kept after the 1 October review
(`culina-v2-kufx.8`): a setup code printed to the log would be one more thing a
self-hoster has to find.
What is done around it: the
connection check is rate limited, answers without raw socket errors, and a
changed database host needs the stored password typed again. The cost: an
operator who publishes the port to the internet and then waits can lose the
instance to whoever scans first. `docs/configuration.md` says to finish setup
before exposing the port.

**Mail is optional, and recovery never depends on it.** Decided 24 October
2026 (`culina-v2-b6l6`, closing `culina-v2-wov4`): the administrator may give
Culina an SMTP account, and then reset links, sign-in links, address
confirmation and notices are mailed. Saved and issued recovery codes stay, so an
expired mail password is a convenience that stops, not the only way back. The
cost that remains: an instance's only administrator who loses both their
password and their saved codes, on an instance without mail, still needs
somebody with database access. With mail on, a new device is recognised by its
kind (`Firefox · macOS`), not a device cookie: a second machine of the same
kind brings no notice, which keeps the notice rare enough to be read.

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
`'self'` plus a nonce. Trusted Types is enforced as well
(`require-trusted-types-for 'script'`, with the three policy names allowed
listed in `SecurityHeaders.TrustedTypesPolicies`), so a string cannot reach
`innerHTML` or the service worker's registration without passing a policy.

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
