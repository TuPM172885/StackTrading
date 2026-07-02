# Glossary

| Thuật ngữ | Ý nghĩa |
| --- | --- |
| `WS2` | Workstream chịu trách nhiệm broker adapters |
| `Broker adapter` | Lớp thực thi thao tác account/order/position/risk trên broker cụ thể |
| `IBrokerAdapter` | Contract chung cho mọi broker adapter |
| `BrokerEvent` | Envelope event chuẩn hóa publish cho downstream |
| `Paper` | Môi trường mô phỏng/simulation |
| `Live` | Môi trường tiền thật hoặc funded/live trading |
| `FCM` | Futures Commission Merchant |
| `FlattenAll` | Đóng toàn bộ vị thế của account |
| `TrimToCompliance` | Giảm vị thế về giới hạn hợp lệ |
| `CorrelationId` | ID truy vết một command/request xuyên hệ thống |
| `IdempotencyKey` | Khóa chống xử lý trùng event/command |
| `DLQ` | Dead-letter queue cho message lỗi |
| `Replay buffer` | Cơ chế phát lại event khi downstream hoặc stream bị gián đoạn |
| `Plant` | Kênh logic trong Rithmic như Order, Tick, History, PnP |
| `MT5 Manager API` | SDK/native API để quản trị MT5 users/accounts/orders |
