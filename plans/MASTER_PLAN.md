# Master Plan WS2

## Mục tiêu

Đưa `WS2` từ trạng thái đã có slice đầu cho `TraderEvolution` lên trạng thái production-ready cho toàn bộ broker gateway layer: `TraderEvolution`, `Rithmic`, `MT5`, cùng contract ổn định, test đầy đủ, CI/CD, bảo mật, observability và vận hành.

## Trạng thái hiện tại

- [x] Có solution `.NET 8` cho `Contracts`, `Application`, `Infrastructure.TraderEvolution`, `Host.Service`.
- [x] Có `TraderEvolution` slice đầu với REST, WebSocket stream, event publish abstraction và fake broker integration test.
- [x] `dotnet test StackTrading.slnx --no-restore` pass baseline gần nhất: `3` unit tests, `1` integration test.
- [x] Có `.gitignore` cho `.NET` build/test artifacts.
- [x] Có hệ thống plan trong `plans/`.

## Roadmap

### Epic 0. Project Planning và Repo Hygiene

- [x] Tạo thư mục `plans/`.
- [x] Lưu master plan, dev plans và code plans.
- [x] Tạo status dashboard.
- [x] Tạo decision log.
- [x] Tạo risk/dependency register.
- [x] Tạo `.gitignore`.
- [ ] Chuẩn hóa encoding Markdown tiếng Việt nếu phát hiện mojibake trong editor/CI.
- [ ] Chạy test baseline sau mỗi đợt chỉnh plan/code đáng kể.

Exit Criteria:

- [x] Tất cả plan files tồn tại theo cấu trúc thống nhất.
- [x] Có checklist theo dõi tiến độ cho `dev` và `code`.
- [x] Repo có nơi rõ ràng để theo dõi plan chính.

### Epic 1. Khóa nền tảng WS2 chung

- [ ] Chốt `IBrokerAdapter` v1.
- [ ] Chốt `BrokerEvent` envelope v1.
- [ ] Chốt error model và retry/cancellation conventions.
- [ ] Tạo compatibility tests áp dụng cho mọi broker.
- [ ] Tài liệu hóa account, order, risk và event lifecycle.

### Epic 2. Hoàn thiện `TraderEvolution`

- [ ] Auth/config thật cho sandbox hoặc broker thật.
- [ ] Mapping DTO broker thật sang domain model.
- [ ] Hoàn thiện order/account/position/risk flows.
- [ ] Hoàn thiện event normalization và reconnect behavior.
- [ ] Paper readiness signoff.
- [ ] Live readiness gate.

### Epic 3. Triển khai `Rithmic`

- [ ] Tạo `Infrastructure.Rithmic`.
- [ ] Xử lý protobuf-over-TCP, heartbeat, session và sequence resync.
- [ ] Tạo plant abstraction.
- [ ] Chuẩn hóa event mapping.
- [ ] Tạo fake harness hoặc sandbox integration tests.

### Epic 4. Triển khai `MT5`

- [ ] Tạo `Infrastructure.MT5`.
- [ ] Thiết kế native wrapper boundary cho Manager API.
- [ ] Kiểm soát memory safety, crash recovery và failover.
- [ ] Mapping account/order/position/state/event.
- [ ] Tạo integration và resilience tests.

### Epic 5. Quality và Test Gate

- [ ] Unit test matrix cho shared runtime và từng broker.
- [ ] Contract tests cho adapter và event schema.
- [ ] Integration tests per broker.
- [ ] Resilience tests cho disconnect, duplicate event, Kafka unavailable, broker unavailable.
- [ ] Coverage gate: chung `>= 80%`, risk/money/event paths `>= 90%`.

### Epic 6. CI/CD, Security và Observability

- [ ] Pipeline restore/build/test/coverage.
- [ ] Package/deploy artifact.
- [ ] Config model cho local/test/staging/prod.
- [ ] Secrets qua `AWS Secrets Manager`.
- [ ] Metrics, traces, dashboards và alerts.
- [ ] DLQ/replay strategy.

### Epic 7. Release Readiness và Handoff

- [ ] Broker readiness checklist.
- [ ] Runbook incident và rollback.
- [ ] Release sequence theo Paper trước Live.
- [ ] Ownership map.
- [ ] Onboarding docs cho dev/QA/ops.

## Liên kết plan con

- Dev plans: `dev/DEV_MASTER_CHECKLIST.md`
- Code plans: `code/CODE_MASTER_CHECKLIST.md`
- Risks: `RISKS_AND_DEPENDENCIES.md`
- Decisions: `DECISIONS.md`
