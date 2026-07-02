# Testing Strategy Plan

## Mục tiêu

Chuẩn hóa test strategy cho toàn bộ WS2.

## Checklist

- [x] Có unit test baseline.
- [x] Có integration test baseline với fake `TraderEvolution`.
- [ ] Unit tests cho mapping.
- [ ] Unit tests cho guard Paper/Live.
- [ ] Unit tests cho retry/error translation.
- [ ] Contract tests cho `IBrokerAdapter`.
- [ ] Schema/compatibility tests cho `BrokerEvent`.
- [ ] Integration harness cho từng broker.
- [ ] Resilience tests cho disconnect/reconnect.
- [ ] Resilience tests cho duplicate event.
- [ ] Failure tests cho Kafka unavailable.
- [ ] Performance smoke tests cho hot path.
- [ ] Coverage gate trong CI.

## Exit Criteria

- [ ] Mỗi broker pass cùng một test matrix.
- [ ] Test suite đủ để chặn regression ở contract/event/risk paths.
