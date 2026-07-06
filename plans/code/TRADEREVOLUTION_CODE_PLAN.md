# TraderEvolution Code Plan

## Mục tiêu

Hoàn thiện `TraderEvolution` adapter từ slice đầu lên trạng thái Paper-ready, sau đó mở readiness gate cho Live.

Adapter phải:

- Dùng đúng TraderEvolution Client API thật.
- Không để broker DTO rò ra `StackTrading.Contracts`.
- Chuẩn hóa account, order, position, account state và broker event về domain model chung.
- Duy trì guard cứng giữa `Paper` và `Live`.
- Có test đủ cho mapping, auth, REST flow, WebSocket subscription, risk action và event publishing.

## Nguồn tham chiếu

- API references: `docs/TRADEREVOLUTION_API_REFERENCES.md`
- API audit hiện tại: `plans/code/TRADEREVOLUTION_API_AUDIT.md`
- User guide: `docs/USER_GUIDE.md`
- Master checklist: `plans/code/CODE_MASTER_CHECKLIST.md`

## Trạng thái hiện tại

### Đã có

- [x] REST client skeleton.
- [x] WebSocket stream skeleton.
- [x] Paper/Live options và validation cơ bản.
- [x] Fake broker integration test.
- [x] Auth scaffold theo Client API: Bearer token, OAuth refresh token, password token và legacy API-key headers.
- [x] REST/WebSocket client có thể gửi `Authorization: Bearer <access_token>`.
- [x] Order status mapping cơ bản.
- [x] Account state mapping cơ bản.
- [x] Position mapping cơ bản.
- [x] `FlattenAllAsync` cho nhiều position bằng reduce/close order flow.
- [x] `TrimToComplianceAsync` theo reduce strategy.
- [x] Event normalization tests.
- [x] Broker error translation tests.
- [x] Đã lưu nguồn API chính thức.
- [x] Đã audit adapter/DTO so với API thật.

### Chưa khớp API thật

- [x] REST endpoint đã chuyển khỏi fake path `/api/...` sang base path `/traderevolution/v1/...` cho các flow hiện có.
- [x] Response reader đã xử lý envelope `s`, `d`, `errmsg`.
- [x] Account DTO đã đọc được `d.accounts[]` và map account settings cơ bản.
- [ ] Order DTO/request chưa khớp Swagger `Trading`.
- [x] Position DTO đã xử lý response dạng array theo sequence mặc định từ docs/config.
- [x] Account state DTO đã xử lý account details dạng array theo sequence mặc định từ docs/config.
- [ ] WebSocket chưa dùng `/stream/tradeEvents` và `/stream/accounts`.
- [ ] WebSocket chưa gửi subscribe/unsubscribe message.
- [ ] WebSocket chưa xử lý `PING`/`PONG`.
- [ ] `FlattenAllAsync` chưa cancel pending orders.
- [ ] Chưa có sandbox credential/token thật để verify Paper.

## Milestone 1: Chuẩn hóa nền Client API

Mục tiêu: đổi adapter từ fake protocol sang shape Client API thật nhưng chưa cần đầy đủ mọi nghiệp vụ.

- [x] Thêm `ApiBasePath`, mặc định `/traderevolution/v1`.
- [x] Đổi config local để `ApiBaseUrl` là host/base URL, còn path API ghép từ `ApiBasePath`.
- [x] Thêm helper build path để tránh hardcode rải rác.
- [x] Thêm DTO envelope:
  - [x] `TraderEvolutionResponse<T>`
  - [ ] `TraderEvolutionDataEnvelope`
  - [x] `TraderEvolutionErrorDto` hỗ trợ `s` và `errmsg`
- [x] Cập nhật `ReadResponseAsync` để:
  - [x] reject `s = error`
  - [x] đọc message từ `errmsg`
  - [x] trả về `d`
  - [ ] giữ raw response trong exception/log khi parse lỗi
- [x] Cập nhật error mapper cho format `s/error/errmsg`.
- [x] Thêm unit tests cho response wrapper và error wrapper.

## Milestone 2: Hoàn thiện auth theo TraderEvolution

Mục tiêu: auth chạy được với sandbox thật hoặc token thật.

- [x] Có auth mode `BearerToken`.
- [x] Có auth mode `OAuthRefreshToken`.
- [x] Có auth mode `Password`.
- [ ] Xác nhận với broker auth type sẽ dùng trong môi trường Paper:
  - [ ] OAuth authorization code
  - [ ] refresh token
  - [ ] password demo flow
  - [ ] external token flow
- [ ] Kiểm tra lại token endpoint path với sandbox thật.
- [x] Thêm token expiry skew cấu hình được.
- [ ] Thêm test token refresh khi token hết hạn.
- [ ] Thêm test auth failure mapping về `AuthenticationFailed`.
- [ ] Không log token/secret trong mọi path lỗi.
- [ ] Cập nhật docs config auth trong `docs/USER_GUIDE.md`.

## Milestone 3: Account và account state

Mục tiêu: đọc account/state từ Client API thật và map về domain.

- [x] Implement `GET /accounts`.
- [x] Thêm `TraderEvolutionAccountsDataDto`.
- [x] Thêm `TraderEvolutionAccountSettingsDto`.
- [x] Map:
  - [x] `id -> BrokerAccount.AccountId`
  - [x] `currency -> BrokerAccount.BaseCurrency`
  - [x] `status -> AccountStatus`
  - [x] `riskRules`, `tradingRules`, `marginRules -> Metadata`
- [ ] Quyết định lại `CreateAccountAsync`:
  - [ ] Nếu Client API không tạo account, map thành lookup existing account theo token/user.
  - [ ] Nếu cần account provisioning, chuyển sang BackOffice API hoặc Middleware Hub boundary.
  - [ ] Ghi quyết định vào `plans/DECISIONS.md`.
- [ ] Quyết định lại `SuspendAccountAsync`:
  - [ ] Nếu thuộc BackOffice API, không fake bằng Client API.
  - [ ] Trả lỗi rõ `NotSupported`/domain equivalent cho tới khi có BackOffice integration.
- [ ] Quyết định lại `CloseAccountAsync`:
  - [ ] Client API có `POST /closeAccount` tạo close request, không đóng ngay.
  - [ ] Map đúng semantic hoặc tách command mới.
- [x] Implement `GET /accounts/{accountId}/state`.
- [x] Đọc `GET /config` hoặc config cố định từ broker để biết `accountDetailsConfig`.
- [x] Map account state sequence sang:
  - [x] `Balance`
  - [x] `Equity` hoặc projected/available equivalent
  - [x] `MarginUsed`
  - [x] `MarginAvailable`
  - [ ] `DailyLoss`
  - [ ] `TrailingDrawdown`
- [x] Unit tests cho account/status/risk metadata mapping.
- [x] Integration test fake broker theo wrapper thật.

## Milestone 4: Orders và positions

Mục tiêu: place/modify/cancel/query order và query positions theo API thật.

- [ ] Đọc Swagger `Trading` để khóa request body cho create order.
- [ ] Implement `POST /accounts/{accountId}/orders`.
- [ ] Map domain `OrderRequest` sang TraderEvolution order request:
  - [ ] accountId path
  - [ ] instrument/tradable instrument id
  - [ ] side
  - [ ] type
  - [ ] quantity
  - [ ] price/stop price
  - [ ] validity/time in force
- [ ] Xử lý bài toán `symbol -> tradableInstrumentId`:
  - [ ] dùng `GET /accounts/{accountId}/instruments`
  - [ ] cache instrument map có TTL
  - [ ] test symbol không tồn tại
- [ ] Implement modify order theo Swagger thật.
- [ ] Implement cancel order theo Swagger thật.
- [ ] Implement `GET /accounts/{accountId}/orders`.
- [ ] Implement `GET /accounts/{accountId}/ordersHistory` khi cần reconcile final orders.
- [ ] Thêm DTO parser cho order rows dạng array sequence.
- [ ] Map order statuses về `OrderStatus`.
- [x] Implement `GET /accounts/{accountId}/positions`.
- [x] Thêm DTO parser cho position rows dạng array sequence.
- [ ] Map:
  - [x] side
  - [x] quantity
  - [x] average price
  - [x] unrealized P/L
  - [ ] instrument name/type/exchange vào metadata hoặc extensions
- [ ] Unit tests cho order row parser.
- [ ] Unit tests cho position row parser.
- [ ] Integration test place/query/cancel với fake broker response wrapper thật.

## Milestone 5: WebSocket subscriptions

Mục tiêu: stream events thật từ TraderEvolution và normalize về `BrokerEvent`.

- [ ] Đổi stream endpoint sang `/stream/tradeEvents`.
- [ ] Đổi account stream endpoint sang `/stream/accounts`.
- [ ] Thêm model subscribe/unsubscribe:
  - [ ] `event`
  - [ ] `requestId`
  - [ ] `payload.accountId`
  - [ ] `payload.st`
- [ ] Subscribe trade events:
  - [ ] `orders`
  - [ ] `openPositions`
  - [ ] `closePositions`
  - [ ] `executions`
  - [ ] `riskRules`
  - [ ] `marginWarning`
  - [ ] `stopOut`
- [ ] Subscribe account events:
  - [ ] `accountDetailsData`
  - [ ] `account`
- [ ] Xử lý `PING` bằng `PONG`.
- [ ] Validate subscribe response `s = ok`.
- [ ] Map subscribe error `s = error` sang `BrokerAdapterException`.
- [ ] Normalize:
  - [ ] `orders -> OrderAccepted/OrderFilled/OrderCancelled/OrderRejected`
  - [ ] `openPositions/closePositions -> PositionUpdated`
  - [ ] `executions -> ExecutionReport`
  - [ ] `riskRules -> DrawdownBreach/MarginBreach` nếu đủ thông tin
  - [ ] `marginWarning -> MarginBreach`
  - [ ] `stopOut -> LiquidationExecuted`
  - [ ] `accountDetailsData/account -> AccountStateChanged`
- [ ] Tạo idempotency key deterministic từ account, event type, broker ids và timestamp.
- [ ] Reconnect phải resubscribe toàn bộ `st`.
- [ ] Unit tests cho event normalization theo từng `st`.
- [ ] Integration test disconnect/reconnect/resubscribe.

## Milestone 6: Risk actions

Mục tiêu: risk actions deterministic, audit được và không bỏ sót pending orders.

- [x] `TrimToComplianceAsync` có reduce strategy.
- [x] `FlattenAllAsync` có reduce strategy cho open positions.
- [ ] `FlattenAllAsync` phải query active orders.
- [ ] `FlattenAllAsync` phải cancel pending orders trước hoặc sau reduce positions theo quyết định risk.
- [ ] Ghi rõ thứ tự risk action:
  - [ ] cancel pending orders
  - [ ] reduce/close open positions
  - [ ] verify positions/orders về trạng thái an toàn
- [ ] Thêm metadata audit cho risk orders:
  - [ ] risk action
  - [ ] reason
  - [ ] requested by
  - [ ] correlation id
  - [ ] target limit
- [ ] Test no-op khi account đã compliant.
- [ ] Test partial trim nhiều position.
- [ ] Test cancel pending orders failure.
- [ ] Test place reduce order failure.

## Milestone 7: Testing và fake broker

Mục tiêu: fake broker phản ánh API thật đủ để tránh test xanh giả.

- [ ] Cập nhật fake broker REST base path `/traderevolution/v1`.
- [ ] Fake broker trả response wrapper `s/d/errmsg`.
- [ ] Fake broker mô phỏng:
  - [ ] `/accounts`
  - [ ] `/accounts/{accountId}/state`
  - [ ] `/accounts/{accountId}/positions`
  - [ ] `/accounts/{accountId}/orders`
  - [ ] order create/modify/cancel
  - [ ] token endpoint
- [ ] Fake broker WebSocket mô phỏng:
  - [ ] `/stream/tradeEvents`
  - [ ] `/stream/accounts`
  - [ ] subscribe response
  - [ ] `PING`/`PONG`
  - [ ] reconnect
- [ ] Unit tests tối thiểu:
  - [ ] auth provider
  - [ ] response wrapper
  - [ ] account mapper
  - [ ] order mapper
  - [ ] position mapper
  - [ ] state mapper
  - [ ] stream event mapper
  - [ ] risk planner
- [ ] Integration tests tối thiểu:
  - [ ] auth + get accounts
  - [ ] place order + order event
  - [ ] query positions
  - [ ] flatten all
  - [ ] reconnect/resubscribe
  - [ ] duplicate event dedupe

## Milestone 8: Observability và operations

Mục tiêu: đủ dấu vết để vận hành Paper.

- [ ] Log structured cho mỗi broker call:
  - [ ] broker
  - [ ] env
  - [ ] endpoint
  - [ ] account id
  - [ ] correlation id
  - [ ] duration
  - [ ] status
- [ ] Không log secrets/tokens.
- [ ] Thêm metrics:
  - [ ] REST call count/error/latency
  - [ ] WebSocket reconnect count
  - [ ] published event count
  - [ ] deduped event count
  - [ ] risk action count/failure
- [ ] Thêm health check mở rộng:
  - [ ] config hợp lệ
  - [ ] Paper enabled
  - [ ] Kafka config nếu enabled
  - [ ] token provider có credential cần thiết
- [ ] Chuẩn bị dashboard/alert trong plan observability chung.

## Milestone 9: Paper readiness

Mục tiêu: ký Paper readiness với sandbox thật.

- [ ] Có sandbox credential/token thật.
- [ ] `Paper.AuthMode` dùng mode thật, không dùng legacy fake `ApiKeyHeaders`.
- [ ] `Live.Enabled = false`.
- [ ] Paper và Live không trùng endpoint/credential.
- [ ] Pass full test suite.
- [ ] Pass manual smoke test:
  - [ ] auth
  - [ ] get accounts
  - [ ] get account state
  - [ ] get positions
  - [ ] place order nhỏ trong sandbox
  - [ ] cancel order
  - [ ] receive order event
  - [ ] receive position/account event
  - [ ] flatten sandbox account
- [ ] Kafka event có đủ:
  - [ ] correlation id
  - [ ] idempotency key
  - [ ] broker
  - [ ] env
  - [ ] normalized payload
- [ ] Cập nhật `docs/USER_GUIDE.md`.
- [ ] Cập nhật `plans/STATUS.md`.

## Blockers

- [ ] Chưa có TraderEvolution sandbox credential/token thật.
- [ ] Chưa chốt auth mode Paper sẽ dùng.
- [ ] Chưa có Swagger/model export đầy đủ cho `Trading` request/response.
- [ ] Chưa chốt account provisioning thuộc Client API, BackOffice API hay Middleware Hub.
- [ ] Chưa chốt mapping `symbol -> tradableInstrumentId`.
- [x] Máy local đã chạy được `dotnet test` sau khi dừng process host service đang khóa file build.

## Thứ tự thực hiện đề xuất

1. Cài/chuẩn hóa .NET 8 runtime để chạy test.
2. Implement response wrapper `s/d/errmsg`.
3. Đổi REST base path sang `/traderevolution/v1`.
4. Implement account/state/positions read flows theo API thật.
5. Implement instrument lookup và order create/cancel.
6. Implement WebSocket trade/account subscription thật.
7. Hoàn thiện risk cancel pending orders.
8. Cập nhật fake broker theo protocol thật.
9. Chạy Paper sandbox thật và ký readiness.

## Exit Criteria

- [x] `dotnet build StackTrading.slnx` pass.
- [x] `dotnet test StackTrading.slnx` pass trên runtime chuẩn.
- [ ] Unit test cover mapper/auth/risk/event paths chính.
- [ ] Integration test dùng fake broker protocol thật pass.
- [ ] Paper sandbox thật pass auth/account/order/query/stream/risk.
- [ ] Event publish đủ header và payload cho downstream.
- [ ] Risk actions deterministic và audit được.
- [ ] Không có credential thật trong repo.
- [ ] `Live` chỉ bật sau khi có Live readiness gate riêng.

## Update Log

### 2026-07-06

- Đã đọc TraderEvolution Client API Authorization process.
- Code đã có token provider để gửi `Authorization: Bearer <access_token>` cho REST và WebSocket.
- Cần sandbox credential/token thật để verify Paper auth end-to-end.
- Đã lưu nguồn API vào `docs/TRADEREVOLUTION_API_REFERENCES.md`.
- Đã rà soát adapter/DTO hiện tại trong `plans/code/TRADEREVOLUTION_API_AUDIT.md`.
- Kết luận audit: REST endpoint, response wrapper, DTO shape và WebSocket protocol còn là skeleton/fake-oriented, chưa khớp API thật.
- Đã bắt đầu thực thi Milestone 1: thêm `ApiBasePath`, đổi REST path sang `/traderevolution/v1`, thêm reader cho envelope `s/d/errmsg`, cập nhật fake broker trả wrapper thật hơn.
- Đã thêm `TokenExpirySkewSeconds` cho auth token cache.
- Đã implement account lookup qua `GET /accounts`, parser position row array và parser account state array theo config mặc định trong docs.
- Test baseline mới: `18` unit tests và `1` integration test pass.
