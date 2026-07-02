# Risks And Dependencies

## Dependencies

| ID | Dependency | Owner | Status | Ghi chú |
| --- | --- | --- | --- | --- |
| DEP-001 | TraderEvolution sandbox/credential | external | blocked | Cần để validate broker thật |
| DEP-002 | Kafka environment/topic | platform | todo | Cần topic, ACL, bootstrap config |
| DEP-003 | Middleware Hub API contract | WS1 | todo | Cần chốt caller boundary |
| DEP-004 | Rithmic API/spec/access | external | todo | Cần docs, credential, simulator hoặc sandbox |
| DEP-005 | MT5 Manager API SDK/native libs | external | todo | Cần SDK, license, runtime target |
| DEP-006 | AWS Secrets Manager setup | platform/security | todo | Cần secret naming convention và IAM |

## Risks

| ID | Risk | Impact | Mitigation | Status |
| --- | --- | --- | --- | --- |
| RISK-001 | Lẫn `Paper` và `Live` config | critical | Hard guard, startup validation, tests | doing |
| RISK-002 | Event duplicate hoặc mất event | high | Idempotency key, dedupe, replay/DLQ plan | doing |
| RISK-003 | Broker stream reconnect không ổn định | high | Resilience tests, backoff, resubscribe | todo |
| RISK-004 | MT5 native wrapper crash/memory issue | critical | Process isolation hoặc strict wrapper boundary | todo |
| RISK-005 | Contract thay đổi làm downstream vỡ | high | Contract tests và versioning policy | todo |
| RISK-006 | Secrets bị commit nhầm | critical | `.gitignore`, secret scanning, Secrets Manager | todo |

## Review cadence

- [ ] Review file này trước mỗi milestone.
- [ ] Mọi blocker mới phải có owner và mitigation.
- [ ] Mọi risk critical phải có test hoặc gate tương ứng.
