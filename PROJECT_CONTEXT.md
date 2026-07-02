# StackTrading Project Context

## Mục đích tài liệu

Tài liệu này tóm tắt bối cảnh dự án `StackTrading` để đội ngũ kỹ thuật, đặc biệt là `.NET Engineer` trong `WS2`, có thể nắm nhanh:

- Sản phẩm đang giải quyết bài toán gì
- Kiến trúc hệ thống vận hành như thế nào
- `WS2` chịu trách nhiệm phần nào
- `broker adapter` cần đáp ứng những yêu cầu gì

Nguồn tổng hợp từ tài liệu gốc: `StackTrading_WS2_Project_Context_Primer_VI_v1.0.docx` ngày `01/07/2026`.

## 1. Tổng quan sản phẩm

`Stack Trading` là một nền tảng `prop-trading` theo mô hình `Direct Market Access (DMA)` cho `Futures` và `Forex`.

Mô hình vận hành chính:

`Simulation-First, then Capital Allocation`

Trader sẽ:

1. Đăng ký và chọn challenge
2. Hoàn tất `KYC`
3. Thanh toán phí challenge
4. Giao dịch trong môi trường mô phỏng (`Simulation` / `Paper`)
5. Được hệ thống liên tục đo hiệu suất và cưỡng chế risk
6. Nếu đạt chuẩn, được nâng lên tài khoản `Live / Funded`

Ngoài phần giao dịch, sản phẩm còn có các yếu tố trải nghiệm như:

- Level-up theo lộ trình nghề nghiệp
- Payout
- Certificate on-chain
- Discord/community
- Audio squawk
- Mentorship pods

## 2. Điều quan trọng với kỹ sư WS2

Business của nền tảng phụ thuộc trực tiếp vào 2 nhóm năng lực kỹ thuật:

1. `Execution` trên từng broker phải chính xác, ổn định và độ trễ thấp
2. `Risk enforcement` phải đúng, deterministic và có thể audit

Điều này đặc biệt quan trọng vì khi trader đã ở môi trường `Live`, hệ thống liên quan trực tiếp đến:

- Tiền thật
- Quản trị rủi ro
- Nghĩa vụ pháp lý và compliance

## 3. Kiến trúc hệ thống tổng quan

StackTrading dùng mô hình `hybrid topology`:

- Phần quản trị và orchestration chạy `cloud-native` trên `AWS`
- Phần execution core cho `Forex` chạy trên hạ tầng riêng tại `Equinix NY4`

Ý tưởng cốt lõi:

- `Middleware Hub` làm vai trò quản trị
- Broker layer và adapter làm vai trò thực thi
- Risk rules được đẩy xuống execution layer để cưỡng chế cục bộ với độ trễ thấp

## 4. Thành phần chính của hệ thống

| Thành phần | Vai trò |
| --- | --- |
| `Middleware Hub` | API trung tâm trên `AWS`, đóng vai trò quản trị và là nơi duy nhất được ghi state |
| `Zapier` | Automation bus, điều phối flow và chuyển đổi trạng thái |
| `RDS PostgreSQL` | System of record cho state machine và trạng thái tài chính |
| `Broker gateways (WS2)` | Nơi triển khai các adapter `TraderEvolution`, `Rithmic`, `MT5` |
| `Kafka` | Event bus nhận event chuẩn hóa từ adapter |
| `Frontend / Dashboard` | Giao diện cho trader, consume `REST` và `WebSocket` |
| `Polygon Ledger` | Sổ cái bất biến cho proof-of-payout, thuộc workstream khác |
| `Observability & Security` | `OpenTelemetry`, `IAM/RBAC`, `WAF`, `GuardDuty`, `CloudTrail`, `Secrets Manager`, kết nối riêng tới `NY4` |

## 5. Luồng dữ liệu end-to-end

Luồng tổng quát đã được đơn giản hóa như sau:

1. Provider bên ngoài như `payment`, `KYC`, `news` gửi dữ liệu vào hệ thống
2. Dữ liệu đi qua `Middleware Hub webhook gateway`
3. `Zapier flows` và `RDS state machine` xử lý orchestration
4. `Middleware Hub` gửi lệnh xuống broker adapter
5. Adapter thực thi trên broker tương ứng
6. Adapter publish event chuẩn hóa lên `Kafka`
7. Các service khác consume event để cập nhật:
   - State machine
   - Ledger
   - Dashboard

## 6. Vai trò của WS2

`WS2` chịu trách nhiệm xây dựng và vận hành lớp `broker adapter`.

Phạm vi chính:

- Xây dựng 3 adapter:
  - `TraderEvolution`
  - `Rithmic`
  - `MT5`
- Tất cả phải implement cùng một contract `IBrokerAdapter`
- Nhận lệnh từ `Middleware Hub`
- Thực hiện thao tác account, order, position và risk control
- Publish event chuẩn hóa lên `Kafka`
- Gắn `OpenTelemetry trace` và `correlation ID`
- Tách biệt chặt chẽ giữa `Paper` và `Live`

## 7. Contract của broker adapter

Tài liệu mô tả một contract minh họa cho `IBrokerAdapter` với các nhóm chức năng chính:

- Vòng đời tài khoản
  - `CreateAccountAsync`
  - `SuspendAccountAsync`
  - `CloseAccountAsync`
- Order và position
  - `PlaceOrderAsync`
  - `ModifyOrderAsync`
  - `CancelOrderAsync`
  - `GetPositionsAsync`
  - `GetAccountStateAsync`
- Cưỡng chế risk
  - `TrimToComplianceAsync`
  - `FlattenAllAsync`
- Streaming event
  - `SubscribeAsync`

Môi trường giao dịch:

- `Paper (Sim)`
- `Live (FCM)`

Việc chọn môi trường được điều khiển bằng config và phải có `runtime guard` cứng để tránh lẫn môi trường.

## 8. Event chuẩn hóa cần publish

Các adapter phải publish các event chuẩn hóa như:

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

Mỗi event cần có:

- `correlation ID`
- `idempotency key`

Mục tiêu là giúp các hệ thống downstream đối soát theo hướng `exactly-once` hoặc gần tương đương về mặt nghiệp vụ.

## 9. Yêu cầu phi chức năng

Đây là các tiêu chí chất lượng chính mà adapter cần đạt:

### 9.1 Latency

- Hot path phải gọn nhẹ
- Lõi `Forex` được đặt tại `NY4` để tối ưu độ trễ

### 9.2 Reliability

- Không được rớt callback tần suất cao
- Cần có `reconnect`
- Cần có `DLQ`
- Cần có `replay buffer`
- Cần `idempotency`

### 9.3 Correctness of risk

- `trim`, `flatten`, `liquidation` phải deterministic
- Thực thi kịp thời theo ngữ cảnh margin và market session

### 9.4 Auditability

- Lưu dấu vết thay đổi state đầy đủ
- Dữ liệu audit mang tính `append-only`
- Thời gian lưu giữ theo tài liệu là `7 năm`

### 9.5 Security

- Secrets lưu trong `AWS Secrets Manager`
- Không để secrets trong `Git`
- Webhook cần có `HMAC`
- Áp dụng `least-privilege`

### 9.6 Test coverage

- Tối thiểu `>= 80%` line/branch coverage
- Các luồng tiền, risk, ledger cần `>= 90%`

## 10. Đặc thù từng broker

| Broker | Transport | Điểm kỹ thuật cần xử lý |
| --- | --- | --- |
| `TraderEvolution` | `REST + WebSocket` | Account, order, position qua `REST`; execution và market-data qua `WebSocket`; risk cấu hình server-side |
| `Rithmic` | `protobuf-over-TCP` | `4 Plants`, heartbeat, handshake, resync sequence-number, guard `Paper/Live`, compliance với `FCM` |
| `MT5 Manager API` | `C++ SDK` qua `P/Invoke` và `REST` | Manager sinks, group rules, balance ops, swap sync, symbol mapping, failover, memory safety cho `P/Invoke`, tích hợp `YourBourse` |

## 11. Những gì thuộc và không thuộc WS2

### Trong scope của WS2

- 3 broker adapter
- Implement `IBrokerAdapter`
- Publish event lên `Kafka`
- `OpenTelemetry`
- Guard `Paper/Live`

### Ngoài scope của WS2

- `Frontend / dashboard`
- `Middleware Hub core`
- `Zapier flows`
- `LLM` phía `WS1`
- `Polygon / Solidity / Web3 ledger`
- Định nghĩa business risk-rule phía server-side

WS2 chịu trách nhiệm cưỡng chế các rule đã được định nghĩa, không phải nơi quyết định rule.

## 12. Thuật ngữ quan trọng

| Thuật ngữ | Ý nghĩa |
| --- | --- |
| `Prop firm / challenge` | Mô hình trader trả phí để chứng minh năng lực trong simulation trước khi được cấp vốn |
| `Sim (Paper)` | Môi trường mô phỏng |
| `Live (FCM)` | Môi trường giao dịch tiền thật có quản lý |
| `Drawdown / trailing drawdown` | Các giới hạn lỗ dùng để đánh giá tài khoản |
| `Overnight / maintenance margin` | Mức vốn yêu cầu để giữ vị thế qua đêm |
| `Flatten-all / trim-to-compliance` | Đóng toàn bộ vị thế hoặc giảm vị thế về mức hợp lệ |
| `Plant` | Kênh logic trong `Rithmic` như `Order`, `Tick`, `History`, `PnP` |
| `Manager API (MT5)` | `C++ SDK` quản trị user và tài khoản `MT5` dùng qua `P/Invoke` |
| `YourBourse Bridge` | Lớp routing và aggregation thanh khoản cho `Forex` tại `NY4` |

## 13. Kết luận kỹ thuật

Nếu nhìn từ góc độ triển khai, dự án này không chỉ là viết adapter để gọi API broker.

Nó là một lớp hạ tầng giao dịch có các yêu cầu đồng thời về:

- Độ đúng nghiệp vụ
- Độ trễ thấp
- Tính an toàn vận hành
- Khả năng audit
- Tính tách biệt môi trường `Sim` và `Live`
- Khả năng tích hợp ổn định với toàn hệ sinh thái event-driven

Nói ngắn gọn: `WS2` là lớp thực thi broker có tính chất mission-critical của toàn bộ nền tảng.

## 14. Tài liệu liên quan

Theo tài liệu gốc, nên đọc thêm:

- `WS2 .NET Engineer Training Plan v1.1 + Checklist`
- `RFQ Prop Tech V6.5`
- `Zapier Integration V6`
- `Website & Dashboard V6`
