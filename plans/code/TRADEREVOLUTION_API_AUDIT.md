# TraderEvolution API Audit

Ngày rà soát: 2026-07-06

Nguồn chính: `docs/TRADEREVOLUTION_API_REFERENCES.md`.

## Kết luận ngắn

Adapter hiện tại vẫn là fake/skeleton-oriented. Phần auth đã có scaffold gần đúng hướng Client API, nhưng REST endpoints, response wrapper, DTO shape và WebSocket subscription chưa khớp API thật.

Rithmic và MT5 chưa có implementation trong `src/`, nên chưa có DTO/adapter để rà soát.

## Những phần đã khớp hướng API

- `TraderEvolutionAuthMode` đã hỗ trợ Bearer token, OAuth refresh token, password token và legacy API-key headers.
- REST/WebSocket client đã có khả năng gửi `Authorization: Bearer <access_token>`.
- Có runtime guard Paper/Live và validation config cơ bản.
- Có mapper domain tách khỏi contract chung, chưa để broker DTO rò ra `Contracts`.

## Những phần chưa khớp API thật

### Endpoint REST

Hiện tại code gọi endpoint fake:

- `POST /api/accounts`
- `POST /api/orders`
- `PATCH /api/orders/{orderId}`
- `DELETE /api/orders/{orderId}`
- `GET /api/accounts/{accountId}/positions`
- `GET /api/accounts/{accountId}/state`

Theo Client API, cần chuyển sang base path `/traderevolution/v1` và endpoint thật, ví dụ:

- `GET /traderevolution/v1/accounts`
- `GET /traderevolution/v1/accounts/{accountId}/orders`
- `GET /traderevolution/v1/accounts/{accountId}/ordersHistory`
- `GET /traderevolution/v1/accounts/{accountId}/positions`
- `GET /traderevolution/v1/accounts/{accountId}/state`
- `POST /traderevolution/v1/accounts/{accountId}/orders`

Các flow `CreateAccountAsync`, `SuspendAccountAsync`, `CloseAccountAsync` hiện chưa thể map 1-1 với Client API. Client API chủ yếu thao tác dưới identity của user đã auth; account creation/suspend có thể thuộc BackOffice API hoặc Middleware Hub boundary, cần quyết định lại.

### Response wrapper

Tài liệu dùng envelope dạng `s`, `d`, `errmsg`. DTO hiện tại deserialize trực tiếp vào domain-like DTO như `TraderEvolutionOrderDto`, `TraderEvolutionPositionDto`, `TraderEvolutionAccountStateDto`.

Cần thêm wrapper DTO:

- `TraderEvolutionResponse<T>`
- `TraderEvolutionListResponse<T>`
- error response có `s = error`, `errmsg`

### Account DTO

API account list trả `d.accounts[]` với field như:

- `id`
- `name`
- `type`
- `currency`
- `status`
- `tradingRules`
- `riskRules`
- `marginRules`

DTO hiện tại dùng:

- `accountId`
- `externalUserId`
- `environment`
- `baseCurrency`
- `balance`
- `equity`
- `createdAt`

Đánh giá: chưa đúng shape API thật. Cần tách DTO account settings khỏi `BrokerAccount`, và bổ sung mapping `id -> AccountId`, `currency -> BaseCurrency`, `status -> AccountStatus`.

### Order DTO và order request

API account orders/trading dùng `accountId` trong path và order/trade records có dạng array theo sequence cấu hình. Ví dụ WebSocket orders update trả `d.orders` là mảng các array, không phải object có `orderId`, `symbol`, `quantity`.

DTO hiện tại giả định object fields:

- `orderId`
- `accountId`
- `environment`
- `status`
- `symbol`
- `side`
- `quantity`
- `filledQuantity`
- `averageFillPrice`

Đánh giá: chưa đúng shape API thật. Cần đọc thêm Swagger/model `OrdersResponse` và `CreateOrder` để map chính xác request/response.

### Position DTO

API positions trả `d.positions` theo array sequence: ID, Instrument ID, Side, Qty, avgPrice, Stop loss ID, Take profit ID, Open date, Unrealized P/L, Strategy ID, Instrument name, Instrument type, Exchange, Stop loss, Take profit.

DTO hiện tại giả định object fields:

- `accountId`
- `symbol`
- `side`
- `quantity`
- `averagePrice`
- `unrealizedPnl`
- `estimatedSwap`
- `updatedAt`

Đánh giá: chưa đúng shape API thật. Cần DTO parse array-based response và map instrument name/type/exchange vào metadata nếu domain chưa có field.

### Account state DTO

API state trả account details theo sequence cấu hình `accountDetailsConfig`; tài liệu mô tả các cột như Balance, Projected balance, Available funds, margin fields, P/L, counts.

DTO hiện tại giả định object fields:

- `balance`
- `equity`
- `marginUsed`
- `marginAvailable`
- `dailyLoss`
- `trailingDrawdown`

Đánh giá: chưa đúng shape API thật. Cần đọc `/config` để lấy column order hoặc khóa mapping theo config được broker cung cấp.

### WebSocket

Code hiện mở:

- `/ws/accounts/{accountId}?env={env}`

Client API thật dùng:

- `/traderevolution/v1/stream/tradeEvents`
- `/traderevolution/v1/stream/accounts`

Sau khi connect, phải gửi message subscribe:

- `{"event":"subscribe","requestId":123,"payload":{"accountId":13466,"st":"orders"}}`
- `st` cho trade events: `orders`, `openPositions`, `closePositions`, `executions`, `riskRules`, `marginWarning`, `stopOut`
- `st` cho account stream: `accountDetailsData`, `account`

Đánh giá: WebSocket hiện chưa đúng protocol. Cần viết subscription client thực sự, xử lý `PING`/`PONG`, subscribe nhiều `st`, normalize event theo `st`.

### Risk actions

`FlattenAllAsync` và `TrimToComplianceAsync` hiện tự đọc positions rồi đặt reduce market orders. Hướng này hợp lý về domain, nhưng endpoint đặt lệnh và position DTO chưa đúng API thật. Pending order cancellation vẫn thiếu vì cần endpoint list/cancel order thật.

## Kế hoạch sửa tiếp theo

1. Thêm cấu hình `ApiBasePath = "/traderevolution/v1"` và đổi sandbox URL để base address là host, path là Client API path.
2. Thêm DTO wrapper `s`/`d`/`errmsg` và cập nhật `ReadResponseAsync`.
3. Tách DTO fake hiện tại khỏi DTO thật hoặc đổi tên thành `FakeTraderEvolution*` trong test.
4. Implement account read flows theo `GET /accounts`, `GET /accounts/{accountId}/state`, `GET /accounts/{accountId}/positions`.
5. Implement order create/modify/cancel theo Swagger `Trading` thật.
6. Implement WebSocket `/stream/tradeEvents` và `/stream/accounts`, gồm subscribe message, ping/pong, normalize `orders`, `positions`, `executions`, `riskRules`, `marginWarning`, `stopOut`.
7. Cập nhật fake broker integration test để mô phỏng response wrapper và stream protocol thật.
8. Khi có sandbox credential, chạy Paper end-to-end và cập nhật checklist Paper readiness.

