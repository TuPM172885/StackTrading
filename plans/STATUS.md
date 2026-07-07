# WS2 Status

## Snapshot

| Area | Status | Ghi chú |
| --- | --- | --- |
| Planning system | done | Đã tạo `plans/`, master/dev/code plans |
| TraderEvolution slice | doing | Slice đầu đã có; mapping/error/event/risk/contract tests pass, fake broker đã gần protocol thật; cần sandbox thật để Paper readiness |
| Contract v1 baseline | done | `IBrokerAdapter`, `BrokerEvent`, enum/error code v1 đã có contract tests |
| Rithmic | todo | Chưa triển khai |
| MT5 | todo | Chưa triển khai |
| CI/CD | todo | Chưa có pipeline |
| Security/compliance | todo | Chưa có secrets/compliance readiness |
| Observability | doing | Có OpenTelemetry cơ bản, cần dashboard/alert |

## Next 3 priorities

- [ ] Hoàn thiện `TraderEvolution` code plan tới Paper readiness.
- [ ] Lấy TraderEvolution sandbox credential/token thật và chạy smoke test Paper.
- [ ] Thiết lập CI test/coverage gate.

## Blockers

- [ ] Chưa có credential/sandbox thật cho `TraderEvolution`.
- [ ] Chưa có spec transport/API chính thức cho `Rithmic` và `MT5`.
- [ ] Chưa có target deployment environment.

## Cập nhật gần nhất

- `2026-07-07`: sửa mapping alias `account.changed`, thêm `BrokerErrorCode.NotSupported`, đồng bộ `SuspendAccountAsync` với D-006, đổi fake broker account lifecycle sang `POST /closeAccount`, bổ sung contract tests cho `IBrokerAdapter`/`BrokerEvent`/error enum v1, fake stream có account event và `PING`, thêm `docs/TRADEREVOLUTION_PAPER_READINESS.md`; `57` unit tests và `1` integration test pass.
- `2026-07-06`: tiếp tục TraderEvolution plan: thêm auth scaffold, `ApiBasePath`, REST path `/traderevolution/v1`, response wrapper `s/d/errmsg`, fake broker wrapper, token expiry skew, account lookup `GET /accounts`, parser positions/state/orders dạng sequence, instrument lookup/cache, `FlattenAllAsync` cancel active orders trước khi flatten, WebSocket `/stream/tradeEvents` + `/stream/accounts` subscribe/resubscribe + `PING/PONG`; `19` unit tests và `1` integration test pass.
- `2026-07-06`: thêm sandbox launch profile và user-secrets setup cho TraderEvolution sandbox; build/test pass, đang chờ sandbox `ClientId`, `ClientSecret`, `RefreshToken`.
- `2026-07-02`: bổ sung TraderEvolution risk reduce planner cho `FlattenAll`/`TrimToCompliance`; test baseline `12` unit, `1` integration pass.
- `2026-07-02`: bổ sung TraderEvolution DTO mapper, event normalization, error translation; test baseline `8` unit, `1` integration pass.
- `2026-07-02`: tạo hệ thống plan và `.gitignore`.
