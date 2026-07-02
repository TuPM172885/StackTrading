# Architecture Foundation Plan

## Mục tiêu

Ổn định nền tảng kỹ thuật dùng chung cho mọi broker adapter.

## Checklist

- [ ] Review và chốt `IBrokerAdapter` v1.
- [ ] Review và chốt domain models v1.
- [ ] Review và chốt `BrokerEvent` envelope v1.
- [ ] Chốt shared error model.
- [ ] Chốt retry/timeout/cancellation conventions.
- [ ] Chốt config model cho broker environments.
- [ ] Chốt tracing/logging conventions.
- [ ] Tạo broker compatibility tests.
- [ ] Tài liệu hóa lifecycle account/order/risk/event.

## Exit Criteria

- [ ] Broker mới không cần đổi contract chung.
- [ ] Downstream có event schema ổn định để consume.
