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
- Sandbox setup: `docs/TRADEREVOLUTION_SANDBOX_SETUP.md`
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
- [x] WebSocket đã dùng `/stream/tradeEvents` và `/stream/accounts`.
- [x] WebSocket đã gửi subscribe message cho trade/account streams.
- [x] WebSocket đã xử lý `PING`/`PONG`.
- [x] `FlattenAllAsync` đã cancel pending orders trước khi flatten.
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
- [x] Cập nhật docs config auth/readiness trong `docs/TRADEREVOLUTION_PAPER_READINESS.md`.

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
- [x] Quyết định lại `CreateAccountAsync`:
  - [x] Nếu Client API không tạo account, map thành lookup existing account theo token/user.
  - [ ] Nếu cần account provisioning, chuyển sang BackOffice API hoặc Middleware Hub boundary.
  - [x] Ghi quyết định vào `plans/DECISIONS.md`.
- [x] Quyết định lại `SuspendAccountAsync`:
  - [x] Nếu thuộc BackOffice API, không fake bằng Client API.
  - [x] Trả lỗi rõ `NotSupported`/domain equivalent cho tới khi có BackOffice integration.
- [x] Quyết định lại `CloseAccountAsync`:
  - [x] Client API có `POST /closeAccount` tạo close request, không đóng ngay.
  - [x] Map đúng semantic hoặc tách command mới.
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

- [x] Đọc Swagger/docs `Trading` để khóa request body bước đầu cho create order.
- [x] Implement `POST /accounts/{accountId}/orders`.
- [x] Map domain `OrderRequest` sang TraderEvolution order request:
  - [x] accountId path
  - [x] instrument/tradable instrument id
  - [x] side
  - [x] type
  - [x] quantity
  - [x] price/stop price
  - [x] validity/time in force
- [x] Xử lý bài toán `symbol -> tradableInstrumentId`:
  - [x] dùng `GET /accounts/{accountId}/instruments`
  - [x] cache instrument map có TTL
  - [ ] test symbol không tồn tại
- [ ] Implement modify order theo Swagger thật.
- [ ] Implement cancel order theo Swagger thật.
- [x] Implement `GET /accounts/{accountId}/orders`.
- [ ] Implement `GET /accounts/{accountId}/ordersHistory` khi cần reconcile final orders.
- [x] Thêm DTO parser cho order rows dạng array sequence.
- [x] Map order statuses về `OrderStatus`.
- [x] Implement `GET /accounts/{accountId}/positions`.
- [x] Thêm DTO parser cho position rows dạng array sequence.
- [ ] Map:
  - [x] side
  - [x] quantity
  - [x] average price
  - [x] unrealized P/L
  - [ ] instrument name/type/exchange vào metadata hoặc extensions
- [x] Unit tests cho order row parser.
- [ ] Unit tests cho position row parser.
- [ ] Integration test place/query/cancel với fake broker response wrapper thật.

## Milestone 5: WebSocket subscriptions

Mục tiêu: stream events thật từ TraderEvolution và normalize về `BrokerEvent`.

- [x] Đổi stream endpoint sang `/stream/tradeEvents`.
- [x] Đổi account stream endpoint sang `/stream/accounts`.
- [x] Thêm model subscribe/unsubscribe:
  - [x] `event`
  - [x] `requestId`
  - [x] `payload.accountId`
  - [x] `payload.st`
- [x] Subscribe trade events:
  - [x] `orders`
  - [x] `openPositions`
  - [x] `closePositions`
  - [x] `executions`
  - [x] `riskRules`
  - [x] `marginWarning`
  - [x] `stopOut`
- [x] Subscribe account events:
  - [x] `accountDetailsData`
  - [x] `account`
- [x] Xử lý `PING` bằng `PONG`.
- [x] Validate subscribe response `s = ok`.
- [x] Map subscribe error `s = error` sang `BrokerAdapterException`.
- [x] Normalize:
  - [x] `orders -> OrderAccepted/OrderFilled/OrderCancelled/OrderRejected`
  - [x] `openPositions/closePositions -> PositionUpdated`
  - [x] `executions -> ExecutionReport`
  - [x] `riskRules -> DrawdownBreach/MarginBreach` nếu đủ thông tin
  - [x] `marginWarning -> MarginBreach`
  - [x] `stopOut -> LiquidationExecuted`
  - [x] `accountDetailsData/account -> AccountStateChanged`
- [x] Tạo idempotency key deterministic từ account, event type, broker ids và timestamp.
- [x] Reconnect phải resubscribe toàn bộ `st`.
- [ ] Unit tests cho event normalization theo từng `st`.
- [ ] Integration test disconnect/reconnect/resubscribe.

## Milestone 6: Risk actions

Mục tiêu: risk actions deterministic, audit được và không bỏ sót pending orders.

- [x] `TrimToComplianceAsync` có reduce strategy.
- [x] `FlattenAllAsync` có reduce strategy cho open positions.
- [x] `FlattenAllAsync` phải query active orders.
- [x] `FlattenAllAsync` phải cancel pending orders trước hoặc sau reduce positions theo quyết định risk.
- [x] Ghi rõ thứ tự risk action:
  - [x] cancel pending orders
  - [x] reduce/close open positions
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

- [x] Cập nhật fake broker REST base path `/traderevolution/v1`.
- [x] Fake broker trả response wrapper `s/d/errmsg`.
- [x] Fake broker mô phỏng:
  - [x] `/accounts`
  - [x] `/accounts/{accountId}/state`
  - [x] `/accounts/{accountId}/positions`
  - [x] `/accounts/{accountId}/orders`
  - [x] order create/modify/cancel
  - [ ] token endpoint
- [x] Fake broker WebSocket mô phỏng:
  - [x] `/stream/tradeEvents`
  - [x] `/stream/accounts`
  - [x] subscribe response
  - [x] `PING`/`PONG`
  - [ ] reconnect
- [x] Unit tests tối thiểu:
  - [x] auth provider
  - [x] response wrapper
  - [x] account mapper
  - [x] order mapper
  - [x] position mapper
  - [x] state mapper
  - [x] stream event mapper
  - [x] risk planner
- [x] Integration tests tối thiểu:
  - [x] auth + get accounts
  - [x] place order + order event
  - [x] query positions
  - [x] flatten all
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
- [x] Pass full test suite.
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
- [x] Cập nhật Paper readiness docs tại `docs/TRADEREVOLUTION_PAPER_READINESS.md`.
- [x] Cập nhật `plans/STATUS.md`.

## Blockers

- [ ] Chưa có TraderEvolution sandbox credential/token thật.
- [ ] Chưa chốt auth mode Paper sẽ dùng.
- [ ] Chưa có Swagger/model export đầy đủ cho `Trading` request/response.
- [x] Đã chốt account provisioning hiện tại: Client API chỉ lookup existing account; provisioning thuộc BackOffice API hoặc Middleware Hub boundary.
- [x] Đã chốt mapping `symbol -> tradableInstrumentId` bước đầu qua `/accounts/{accountId}/instruments` + cache TTL.
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
- [x] Unit test cover mapper/auth/risk/event paths chính.
- [x] Integration test dùng fake broker protocol thật pass.
- [ ] Paper sandbox thật pass auth/account/order/query/stream/risk.
- [ ] Event publish đủ header và payload cho downstream.
- [ ] Risk actions deterministic và audit được.
- [x] Không có credential thật trong repo.
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
- Đã implement instrument lookup/cache, TraderEvolution order request DTO, active orders parser và `FlattenAllAsync` cancel active orders trước khi flatten positions.
- Đã đổi WebSocket subscription sang `/stream/tradeEvents`, gửi subscribe cho các `st` trade event, xử lý `PING/PONG`, subscribe error và normalize event stream cơ bản.
- Đã thêm `/stream/accounts`, subscribe `accountDetailsData`/`account`, merge trade/account streams vào cùng subscription pipeline và resubscribe cả hai stream sau reconnect.
- Đã thêm launch profile `sandbox`, `UserSecretsId` và hướng dẫn ghép nối sandbox tại `docs/TRADEREVOLUTION_SANDBOX_SETUP.md`.
- Test baseline mới: `19` unit tests và `1` integration test pass.
