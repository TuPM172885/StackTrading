# Hướng dẫn sử dụng StackTrading WS2

## 1. Dự án này là gì?

`StackTrading WS2` là service adapter giao dịch theo mô hình `standalone service`.

Mục tiêu của dự án là đứng giữa `Middleware Hub` và các broker trading, nhận command/query từ hệ thống nội bộ, gọi API broker tương ứng, chuẩn hóa kết quả về domain model chung, sau đó publish các sự kiện giao dịch ra Kafka cho downstream consume.

Ở giai đoạn hiện tại, broker đầu tiên được triển khai là `TraderEvolution`. Các broker sau như `Rithmic` và `MT5` sẽ dùng lại cùng contract, host service, observability và pattern publish event.

Luồng tổng quát:

```text
Middleware Hub
  -> HTTP JSON api/v1
  -> StackTrading.Host.Service
  -> IBrokerAdapter
  -> TraderEvolution REST/WebSocket
  -> BrokerEvent chuẩn hóa
  -> Kafka
```

## 2. Công nghệ sử dụng

Dự án hiện sử dụng các công nghệ chính sau:

- `.NET 8`: runtime và SDK chính.
- `ASP.NET Core`: host HTTP API, controllers, health checks và Swagger.
- `System.Text.Json`: serialize/deserialize JSON.
- `Confluent.Kafka`: publish broker events ra Kafka.
- `OpenTelemetry`: traces và metrics cơ bản cho HTTP, HttpClient và runtime.
- `Swashbuckle.AspNetCore`: tạo Swagger UI trong môi trường Development.
- `xUnit`: test framework.
- `FluentAssertions`: assertion library cho unit/integration tests.
- `Microsoft.AspNetCore.Mvc.Testing`: integration test host ASP.NET Core.

## 3. Kiến trúc solution

Solution được chia theo hướng clean-but-pragmatic:

```text
StackTrading.slnx
src/
  StackTrading.Contracts/
  StackTrading.Application/
  StackTrading.Infrastructure.TraderEvolution/
  StackTrading.Host.Service/
tests/
  StackTrading.Tests.Unit/
  StackTrading.Tests.Integration/
plans/
docs/
```

Ý nghĩa từng thư mục chính:

- `src/StackTrading.Contracts`: chứa contract dùng chung, domain models, enums, `IBrokerAdapter`, exception và mã lỗi domain.
- `src/StackTrading.Application`: chứa orchestration layer, dedupe event, subscription registry và publisher abstraction.
- `src/StackTrading.Infrastructure.TraderEvolution`: chứa REST/WebSocket client, config, Paper/Live guard, DTO broker, mapper và error translation cho `TraderEvolution`.
- `src/StackTrading.Host.Service`: service ASP.NET Core, HTTP API, DI, background stream worker, Kafka publisher, health check và OpenTelemetry.
- `tests/StackTrading.Tests.Unit`: unit tests cho orchestrator, config validation, mapper và error translation.
- `tests/StackTrading.Tests.Integration`: integration test với fake `TraderEvolution` broker server.
- `plans/`: master plan, dev plan, code plan, status dashboard, risks, decisions và glossary.
- `docs/`: tài liệu hướng dẫn sử dụng và vận hành.

## 4. Các khái niệm quan trọng

- `TradingEnv`: môi trường giao dịch, gồm `Paper` và `Live`.
- `Paper`: môi trường bắt buộc cho phase đầu, dùng để test và verify flow an toàn.
- `Live`: môi trường giao dịch thật, hiện có config/guard nhưng mặc định đang tắt.
- `IBrokerAdapter`: contract chuẩn mà mọi broker adapter phải implement.
- `BrokerEvent`: envelope chuẩn cho sự kiện publish ra Kafka.
- `CorrelationId`: mã theo dõi request end-to-end. Nếu caller không truyền, service sẽ tự sinh.
- `IdempotencyKey`: khóa chống xử lý trùng event.
- `FlattenAll`: đóng toàn bộ vị thế của account.
- `TrimToCompliance`: giảm vị thế để đưa account về trạng thái tuân thủ rule/risk.

## 5. Yêu cầu môi trường

Cần cài đặt:

- `.NET SDK 8`.
- Git.
- Kafka nếu muốn bật publish event thật.
- Credential/endpoint `TraderEvolution` nếu muốn kết nối broker thật.

Kiểm tra .NET:

```powershell
dotnet --version
```

## 6. Cấu hình ứng dụng

File cấu hình chính:

```text
src/StackTrading.Host.Service/appsettings.json
```

Cấu hình mặc định hiện tại:

```json
{
  "Kafka": {
    "BootstrapServers": "",
    "Topic": "broker-events-traderevolution",
    "Enabled": false
  },
  "TraderEvolution": {
    "RestRetryCount": 3,
    "StreamReconnectDelaySeconds": 3,
    "PreconfiguredSubscriptions": [],
    "Paper": {
      "ApiBaseUrl": "http://localhost:5501",
      "WebSocketBaseUrl": "ws://localhost:5501",
      "ApiKey": "paper-key",
      "ApiSecret": "paper-secret",
      "TimeoutSeconds": 10,
      "Enabled": true
    },
    "Live": {
      "ApiBaseUrl": "http://localhost:6501",
      "WebSocketBaseUrl": "ws://localhost:6501",
      "ApiKey": "live-key",
      "ApiSecret": "live-secret",
      "TimeoutSeconds": 10,
      "Enabled": false
    }
  }
}
```

Lưu ý quan trọng:

- Không commit credential thật vào repo.
- `Paper` phải bật để hoàn thành broker đầu tiên.
- `Live` mặc định tắt. Chỉ bật khi đã có credential thật, compliance readiness và approval nội bộ.
- `Paper` và `Live` không được dùng chung endpoint/API key.
- Nếu bật Kafka, cần cấu hình `Kafka:Enabled = true` và `Kafka:BootstrapServers`.

## 7. Cách chạy local

Restore/build/test:

```powershell
dotnet restore StackTrading.slnx
dotnet build StackTrading.slnx
dotnet test StackTrading.slnx
```

Chạy service:

```powershell
dotnet run --project src/StackTrading.Host.Service
```

Khi chạy ở môi trường Development, Swagger UI được bật. URL cụ thể phụ thuộc `launchSettings.json` hoặc output của `dotnet run`, thường có dạng:

```text
http://localhost:<port>/swagger
```

Health check:

```powershell
curl http://localhost:<port>/health
```

## 8. HTTP API chính

Base route:

```text
/api/v1
```

Các endpoint hiện có:

- `POST /api/v1/accounts`: tạo account broker.
- `POST /api/v1/accounts/{accountId}/suspend?env=Paper&correlationId=...`: suspend account.
- `DELETE /api/v1/accounts/{accountId}?env=Paper&correlationId=...`: close account.
- `POST /api/v1/orders`: đặt lệnh.
- `PATCH /api/v1/orders/{orderId}`: sửa lệnh.
- `DELETE /api/v1/orders/{orderId}?accountId=...&env=Paper&correlationId=...`: hủy lệnh.
- `GET /api/v1/accounts/{accountId}/positions?env=Paper&correlationId=...`: lấy positions.
- `GET /api/v1/accounts/{accountId}/state?env=Paper&correlationId=...`: lấy account state.
- `POST /api/v1/accounts/{accountId}/risk/trim`: trim account theo compliance/risk.
- `POST /api/v1/accounts/{accountId}/risk/flatten`: flatten toàn bộ vị thế.
- `POST /api/v1/accounts/{accountId}/subscriptions?env=Paper`: đăng ký stream event cho account.

Ví dụ tạo account:

```powershell
curl -X POST http://localhost:<port>/api/v1/accounts `
  -H "Content-Type: application/json" `
  -d '{
    "environment": "Paper",
    "externalUserId": "user-1",
    "challengeId": "challenge-1",
    "baseCurrency": "USD",
    "startingBalance": 100000,
    "correlationId": "corr-create-1",
    "metadata": {
      "source": "local-test"
    }
  }'
```

Ví dụ đặt lệnh:

```powershell
curl -X POST http://localhost:<port>/api/v1/orders `
  -H "Content-Type: application/json" `
  -d '{
    "accountId": "ACC-1",
    "environment": "Paper",
    "symbol": "EURUSD",
    "side": "Buy",
    "type": "Market",
    "quantity": 1,
    "limitPrice": null,
    "stopPrice": null,
    "timeInForce": "Day",
    "correlationId": "corr-order-1",
    "metadata": null,
    "extensions": null
  }'
```

Ví dụ lấy positions:

```powershell
curl "http://localhost:<port>/api/v1/accounts/ACC-1/positions?env=Paper&correlationId=corr-read-1"
```

Ví dụ flatten:

```powershell
curl -X POST http://localhost:<port>/api/v1/accounts/ACC-1/risk/flatten `
  -H "Content-Type: application/json" `
  -d '{
    "environment": "Paper",
    "reason": "manual-risk-action",
    "requestedBy": "ops-user",
    "targetLimit": null,
    "correlationId": "corr-flatten-1",
    "metadata": {
      "ticket": "INC-1"
    }
  }'
```

## 9. Kafka event

Khi Kafka được bật, service publish `BrokerEvent` vào topic:

```text
broker-events-traderevolution
```

Topic có thể đổi trong cấu hình:

```json
{
  "Kafka": {
    "Topic": "broker-events-traderevolution"
  }
}
```

Các event type v1:

- `OrderAccepted`
- `OrderFilled`
- `OrderCancelled`
- `OrderRejected`
- `PositionUpdated`
- `AccountStateChanged`
- `ExecutionReport`
- `MarginBreach`
- `DrawdownBreach`
- `LiquidationExecuted`

Kafka key hiện theo `accountId`. Header dự kiến dùng để downstream đối soát gồm:

- `correlationId`
- `idempotencyKey`
- `broker`
- `env`

## 10. Test

Chạy toàn bộ test:

```powershell
dotnet test StackTrading.slnx
```

Chạy riêng unit tests:

```powershell
dotnet test tests/StackTrading.Tests.Unit/StackTrading.Tests.Unit.csproj
```

Chạy riêng integration tests:

```powershell
dotnet test tests/StackTrading.Tests.Integration/StackTrading.Tests.Integration.csproj
```

Integration test hiện dùng fake `TraderEvolution` broker server, bao gồm REST và WebSocket flow cơ bản. Vì vậy có thể verify end-to-end mà chưa cần credential broker thật.

## 11. Cách phát triển tiếp

Trước khi code tiếp, nên đọc:

```text
plans/STATUS.md
plans/MASTER_PLAN.md
plans/code/TRADEREVOLUTION_CODE_PLAN.md
plans/dev/TRADEREVOLUTION_DELIVERY_PLAN.md
```

Quy trình khuyến nghị:

1. Chọn task trong `plans/STATUS.md` hoặc plan con tương ứng.
2. Implement trong layer đúng trách nhiệm.
3. Thêm hoặc cập nhật tests.
4. Chạy `dotnet test StackTrading.slnx`.
5. Cập nhật checklist trong `plans/`.
6. Commit thay đổi với message rõ ràng.

## 12. Quy ước phát triển

- Giữ contract chung trong `StackTrading.Contracts` sạch, không để raw broker DTO rò ra ngoài.
- Broker-specific fields chỉ đi qua `metadata` hoặc `extensions`.
- Mọi command nên có `correlationId`.
- Không hardcode secrets thật.
- Không bật `Live` nếu chưa qua readiness gate.
- Khi thêm broker mới, tạo infrastructure module riêng nhưng dùng lại `IBrokerAdapter` và `BrokerEvent`.
- Khi sửa logic tiền/risk/event, phải có test đi kèm.

## 13. Trạng thái hiện tại

Tại thời điểm viết tài liệu này:

- Đã có solution `.NET 8`.
- Đã có `TraderEvolution` adapter slice đầu.
- Đã có REST/WebSocket skeleton, DTO mapper, event normalization và error translation.
- Đã có unit/integration test baseline.
- Kafka publisher đã có, mặc định đang tắt trong local config.
- `Paper` đang là môi trường chính.
- `Live` chưa enable thật.
- `Rithmic` và `MT5` chưa triển khai.

## 14. Troubleshooting nhanh

Nếu app không start:

- Kiểm tra `TraderEvolution:Paper` có đủ `ApiBaseUrl`, `WebSocketBaseUrl`, `ApiKey`, `ApiSecret`.
- Kiểm tra endpoint `Paper` và `Live` không bị trùng.
- Kiểm tra `Paper:Enabled` đang là `true`.

Nếu gọi API bị lỗi `502`:

- Kiểm tra broker endpoint có chạy không.
- Kiểm tra API key/secret.
- Kiểm tra `env` request có đúng `Paper` hoặc `Live` không.
- Xem log để lấy `correlationId` và message lỗi broker.

Nếu không thấy Kafka event:

- Kiểm tra `Kafka:Enabled`.
- Kiểm tra `Kafka:BootstrapServers`.
- Kiểm tra đã gọi endpoint subscription hoặc cấu hình `PreconfiguredSubscriptions`.
- Kiểm tra downstream consume đúng topic.

Nếu WebSocket stream không ổn định:

- Kiểm tra `TraderEvolution:StreamReconnectDelaySeconds`.
- Kiểm tra broker WebSocket URL.
- Kiểm tra account đã được đăng ký subscription chưa.
