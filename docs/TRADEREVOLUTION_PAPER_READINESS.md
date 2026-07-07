# TraderEvolution Paper Readiness

## Current status

Paper readiness is prepared but blocked until real TraderEvolution sandbox credentials are available.

Latest verified local baseline:

- `dotnet test StackTrading.slnx --no-restore`
- `57` unit tests pass.
- `1` integration test pass.
- Fake broker uses `/traderevolution/v1`, `s/d/errmsg` response wrappers, trade/account WebSocket streams, subscribe ack, `PING/PONG`, account events, order events, query positions, flatten, and `POST /closeAccount`.

## Required secrets

Store these with user-secrets for `src/StackTrading.Host.Service`; do not commit them:

```powershell
dotnet user-secrets set "TraderEvolution:Paper:ClientId" "<client-id>" --project src/StackTrading.Host.Service
dotnet user-secrets set "TraderEvolution:Paper:ClientSecret" "<client-secret>" --project src/StackTrading.Host.Service
dotnet user-secrets set "TraderEvolution:Paper:RefreshToken" "<refresh-token>" --project src/StackTrading.Host.Service
```

## Required config

- `TraderEvolution:Paper:AuthMode = OAuthRefreshToken`, unless the broker confirms another real Paper auth mode.
- `TraderEvolution:Live:Enabled = false`.
- Paper and Live endpoints/credentials must not match.
- Kafka can stay disabled for the first sandbox smoke test.

## Smoke test checklist

- Auth succeeds.
- `POST /api/v1/accounts` returns an existing TraderEvolution account.
- `GET /api/v1/accounts/{accountId}/state` succeeds.
- `GET /api/v1/accounts/{accountId}/positions` succeeds.
- `POST /api/v1/orders` places a small sandbox order.
- `DELETE /api/v1/orders/{orderId}` cancels the sandbox order.
- `POST /api/v1/accounts/{accountId}/subscriptions` receives order and account events.
- `POST /api/v1/accounts/{accountId}/risk/flatten` flattens the sandbox account.
- `git diff` contains no real credentials or tokens.

## Signoff rule

Paper can be signed off only after the real sandbox smoke test passes. Live remains disabled until a separate Live readiness gate is completed.
