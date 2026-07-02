# Tech Debt Hardening Plan

## Mục tiêu

Theo dõi các việc làm chắc hệ thống sau khi slice đầu đã chạy.

## Checklist

- [ ] Review concurrency trong stream background service.
- [ ] Review cancellation propagation.
- [ ] Review retry policy cho non-idempotent operations.
- [ ] Review memory growth của in-memory dedupe.
- [ ] Review Kafka producer lifecycle.
- [ ] Review health check coverage.
- [ ] Review structured logging fields.
- [ ] Review exception mapping ra HTTP status.
- [ ] Review generated artifacts không vào source control.
- [ ] Thêm analyzers/formatting nếu team chốt.

## Exit Criteria

- [ ] Không còn known technical debt chặn Paper readiness.
- [ ] Các hardening item critical có test hoặc documented decision.
