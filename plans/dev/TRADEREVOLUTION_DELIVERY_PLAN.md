# TraderEvolution Delivery Plan

## Mục tiêu

Đưa `TraderEvolution` từ slice đầu hiện tại lên Paper-ready, sau đó chuẩn bị Live readiness gate.

## Checklist

- [x] Có slice đầu với fake broker integration test.
- [ ] Có sandbox/credential thật.
- [ ] Paper config được validate.
- [ ] Kafka topic/config sẵn sàng.
- [ ] Secrets không nằm trong repo.
- [ ] Dashboard/alert cơ bản sẵn sàng.
- [ ] Runbook auth failure và reconnect issue.
- [ ] Rollback path được định nghĩa.
- [ ] Paper signoff hoàn tất.
- [ ] Live readiness prerequisites hoàn tất.

## Dependencies

- `code/TRADEREVOLUTION_CODE_PLAN.md`
- `dev/ENVIRONMENT_READINESS_PLAN.md`
- `dev/SECURITY_COMPLIANCE_PLAN.md`

## Exit Criteria

- [ ] Paper flow chạy với broker sandbox thật.
- [ ] Risk actions được audit và có test.
- [ ] Có signoff trước khi bật Live.
