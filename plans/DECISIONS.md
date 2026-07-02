# Decisions

Ghi lại các quyết định quan trọng để team không phải suy đoán lại bối cảnh sau này.

| ID | Ngày | Quyết định | Lý do | Tác động |
| --- | --- | --- | --- | --- |
| D-001 | 2026-07-02 | Broker đầu tiên là `TraderEvolution` | Khớp tài liệu dự án và slice code hiện tại | Dùng làm template cho `Rithmic` và `MT5` |
| D-002 | 2026-07-02 | `plans/` là nguồn theo dõi tiến độ chính | Cần tách roadmap, dev delivery và code implementation | Mọi task lớn phải gắn vào plan |
| D-003 | 2026-07-02 | `dev plan` = delivery/QA/release/ops; `code plan` = implementation kỹ thuật | Tránh trộn vận hành với chi tiết code | Mỗi plan có ownership rõ |
| D-004 | 2026-07-02 | `Paper` phải đi trước `Live` cho mọi broker | Giảm rủi ro tiền thật và compliance | Live cần readiness gate riêng |

## Decision backlog

- [ ] Chọn strategy chính thức cho HTTP vs gRPC boundary với Middleware Hub.
- [ ] Chọn event schema registry/versioning approach.
- [ ] Chọn deployment target cho WS2 service.
- [ ] Chọn DLQ/replay design cho Kafka publish failures.
