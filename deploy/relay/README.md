# Push relay

The small service that lets a self-hosted Culina server say "your recipe is
ready" to the iOS and Android apps. Apple and Google accept pushes only from the
publisher of an app, so the publisher runs this, holding the APNs key and the
Firebase service account; a Culina server never sees either. Design and
reasoning: `docs/native-app-design.md`, part 7.

It knows devices, not servers or people. A relay token stands for one device;
only its SHA-256 is stored, so a copy of the database cannot push to anybody.
It keeps counters (meter `Culina.Relay`), never what a notification said.

## API

| Method | Path | |
| --- | --- | --- |
| `POST` | `/v1/devices` | `{platform: ios\|android, deviceToken}` from the app. `201 {relayToken}`: 32 random bytes, base64url. |
| `DELETE` | `/v1/devices/{relayToken}` | The app, on sign-out. `204`, also for one already gone. |
| `POST` | `/v1/notifications` | `{relayToken, title, body, path, tag?}` from a Culina server. `202` handed to the store; `410` unknown, revoked or dropped by the store (the server deletes its row); `429` over the limit; `502` the store could not take it now (the server retries). `path` must be the app's own (`/…`, not `//…`). |
| `GET` | `/healthz` | `200`. |

Limits: `Relay:PerHour` (30) per relay token and per sending address, and as
many registrations an hour per address; a fixed window in memory.

## Configuration

| Setting | |
| --- | --- |
| `Relay__DatabasePath` | The SQLite file; `/data/relay.db` in the image. Mount `/data`. |
| `Relay__PerHour` | Defaults to 30. |
| `Relay__BehindProxy` | `true` when one proxy in front sets `X-Forwarded-For`. |
| `Relay__Apns__TeamId`, `__KeyId`, `__PrivateKeyPath`, `__Topic` | The `.p8` token key and the app's bundle id. `__Sandbox=true` for development builds. |
| `Relay__Fcm__ProjectId`, `__ServiceAccountPath` | The Firebase project and its service account JSON. |

At least one of APNs and FCM is required; the relay refuses to start otherwise,
or when a key file is missing. Mount the key files read-only, as secrets.

## Run

```bash
docker build -f deploy/relay/Dockerfile -t culina-relay .
docker run -d -p 8080:8080 -v relay-data:/data -v /secure/keys:/keys:ro \
  -e Relay__Apns__TeamId=… -e Relay__Apns__KeyId=… \
  -e Relay__Apns__PrivateKeyPath=/keys/AuthKey.p8 -e Relay__Apns__Topic=app.culina \
  -e Relay__Fcm__ProjectId=… -e Relay__Fcm__ServiceAccountPath=/keys/fcm.json \
  culina-relay
```

Put it behind TLS. An outage only delays "import ready" notifications: servers
keep the notification in their outbox and retry.
