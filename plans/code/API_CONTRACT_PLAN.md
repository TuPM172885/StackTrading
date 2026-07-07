# API Contract Plan

## Mục tiêu

Chốt contract giữa Middleware Hub, WS2 host và downstream event consumers.

## Checklist

- [x] Chốt baseline HTTP API v1 cho account lifecycle.
- [x] Chốt baseline HTTP API v1 cho order lifecycle.
- [x] Chốt baseline HTTP API v1 cho account/position query.
- [x] Chốt baseline HTTP API v1 cho risk actions.
- [x] Chốt `IBrokerAdapter` v1 method signatures.
- [x] Chốt `BrokerEvent` envelope v1.
- [ ] Chốt API/event versioning policy.
- [x] Chốt compatibility test snapshots cho enum/error code/event envelope v1.
- [ ] Ghi rõ broker-specific fields qua `metadata`/`extensions`.
- [x] Tài liệu hóa baseline error code shape trong `BrokerErrorCode`.

## Exit Criteria

- [ ] Middleware Hub có thể tích hợp mà không cần biết raw broker DTO.
- [ ] Downstream có schema ổn định để consume event.
