# TraderEvolution Sandbox Setup

File này hướng dẫn ghép nối `StackTrading.Host.Service` với TraderEvolution sandbox mà không lưu credential thật vào repo.

## Endpoint sandbox

- REST host: `https://sandbox-api.traderevolution.com`
- WebSocket host: `wss://sandbox-api.traderevolution.com`
- API base path: `/traderevolution/v1`
- Swagger thật của TraderEvolution: `https://sandbox-api.traderevolution.com/traderevolution/v1/swagger-ui/#/`

## Cấu hình secret local

Chạy trong thư mục repo:

```powershell
dotnet user-secrets set "TraderEvolution:Paper:ClientId" "<client-id>" --project src/StackTrading.Host.Service
dotnet user-secrets set "TraderEvolution:Paper:ClientSecret" "<client-secret>" --project src/StackTrading.Host.Service
dotnet user-secrets set "TraderEvolution:Paper:RefreshToken" "<refresh-token>" --project src/StackTrading.Host.Service
```

Profile `sandbox` đã cấu hình sẵn:

- `AuthMode = OAuthRefreshToken`
- `Paper` trỏ tới sandbox
- `Live.Enabled = false`
- `Kafka.Enabled = false`

## Chạy service với sandbox

```powershell
dotnet run --project src/StackTrading.Host.Service --launch-profile sandbox
```

Sau đó mở:

```text
http://localhost:5111/swagger
```

## Smoke test qua Swagger local

Thứ tự test đề xuất:

1. `POST /api/v1/accounts`
2. `GET /api/v1/accounts/{accountId}/state`
3. `GET /api/v1/accounts/{accountId}/positions`
4. `POST /api/v1/orders`
5. `DELETE /api/v1/orders/{orderId}`
6. `POST /api/v1/accounts/{accountId}/subscriptions`
7. `POST /api/v1/accounts/{accountId}/risk/flatten`

## Lưu ý an toàn

- Không commit `ClientSecret`, `RefreshToken`, `AccessToken`.
- Chỉ dùng `Paper` khi test sandbox.
- Không bật `Live` nếu chưa qua readiness gate.
- Nếu token hết hạn hoặc bị revoke, cập nhật lại `RefreshToken` trong user-secrets.
