# API Contract Plan

## Mục tiêu

Chốt contract giữa Middleware Hub, WS2 host và downstream event consumers.

## Checklist

- [ ] Chốt HTTP API v1 cho account lifecycle.
- [ ] Chốt HTTP API v1 cho order lifecycle.
- [ ] Chốt HTTP API v1 cho account/position query.
- [ ] Chốt HTTP API v1 cho risk actions.
- [ ] Chốt `IBrokerAdapter` v1 method signatures.
- [ ] Chốt `BrokerEvent` envelope v1.
- [ ] Chốt API/event versioning policy.
- [ ] Chốt compatibility test snapshots.
- [ ] Ghi rõ broker-specific fields qua `metadata`/`extensions`.
- [ ] Tài liệu hóa error response shape.

## Exit Criteria

- [ ] Middleware Hub có thể tích hợp mà không cần biết raw broker DTO.
- [ ] Downstream có schema ổn định để consume event.
