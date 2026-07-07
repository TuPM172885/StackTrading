# Testing Strategy Plan

## Mục tiêu

Chuẩn hóa test strategy cho toàn bộ WS2.

## Checklist

- [x] Có unit test baseline.
- [x] Có integration test baseline với fake `TraderEvolution`.
- [x] Unit tests cho mapping.
- [x] Unit tests cho guard Paper/Live.
- [ ] Unit tests cho retry/error translation.
- [x] Contract tests cho `IBrokerAdapter`.
- [x] Schema/compatibility tests cho `BrokerEvent`.
- [ ] Integration harness cho từng broker.
- [ ] Resilience tests cho disconnect/reconnect.
- [x] Resilience tests cho duplicate event.
- [ ] Failure tests cho Kafka unavailable.
- [ ] Performance smoke tests cho hot path.
- [ ] Coverage gate trong CI.

## Exit Criteria

- [ ] Mỗi broker pass cùng một test matrix.
- [ ] Test suite đủ để chặn regression ở contract/event/risk paths.
