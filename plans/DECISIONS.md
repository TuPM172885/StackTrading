# Decisions

Ghi lại các quyết định quan trọng để team không phải suy đoán lại bối cảnh sau này.

| ID | Ngày | Quyết định | Lý do | Tác động |
| --- | --- | --- | --- | --- |
| D-001 | 2026-07-02 | Broker đầu tiên là `TraderEvolution` | Khớp tài liệu dự án và slice code hiện tại | Dùng làm template cho `Rithmic` và `MT5` |
| D-002 | 2026-07-02 | `plans/` là nguồn theo dõi tiến độ chính | Cần tách roadmap, dev delivery và code implementation | Mọi task lớn phải gắn vào plan |
| D-003 | 2026-07-02 | `dev plan` = delivery/QA/release/ops; `code plan` = implementation kỹ thuật | Tránh trộn vận hành với chi tiết code | Mỗi plan có ownership rõ |
| D-004 | 2026-07-02 | `Paper` phải đi trước `Live` cho mọi broker | Giảm rủi ro tiền thật và compliance | Live cần readiness gate riêng |

| D-005 | 2026-07-07 | `CreateAccountAsync` (TraderEvolution) = lookup tài khoản hiện có qua `GET /accounts`, không tạo mới | Client API không có endpoint tạo account; provisioning thuộc BackOffice API hoặc Middleware Hub boundary | Nếu cần provisioning thật, phải tích hợp riêng BackOffice API — nằm ngoài scope WS2 hiện tại |
| D-006 | 2026-07-07 | `SuspendAccountAsync` (TraderEvolution) = trả `NotSupported` cho đến khi có BackOffice integration | Client API không có endpoint suspend; không fake bằng cách gọi endpoint sai | Downstream phải xử lý `BrokerErrorCode.NotSupported` khi cần suspend account |
| D-007 | 2026-07-07 | `CloseAccountAsync` (TraderEvolution) = gọi `POST /accounts/{id}/closeAccount` tạo close request, không đóng ngay | Client API có endpoint này nhưng là async request, không phải đóng tức thì | Semantic khác với `IBrokerAdapter.CloseAccountAsync` — cần document rõ cho caller |

## Decision backlog

- [ ] Chọn strategy chính thức cho HTTP vs gRPC boundary với Middleware Hub.
- [ ] Chọn event schema registry/versioning approach.
- [ ] Chọn deployment target cho WS2 service.
- [ ] Chọn DLQ/replay design cho Kafka publish failures.
