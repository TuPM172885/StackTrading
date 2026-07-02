# MT5 Delivery Plan

## Mục tiêu

Chuẩn bị delivery path cho `MT5` với trọng tâm native dependency, failover và vận hành an toàn.

## Checklist

- [ ] Có MT5 Manager API SDK/native libs.
- [ ] Chốt host OS/runtime requirement.
- [ ] Chốt license và deployment constraint.
- [ ] Có Paper/test server.
- [ ] Có failover environment.
- [ ] Có security/compliance checklist.
- [ ] Có crash recovery runbook.
- [ ] Có release gate.

## Dependencies

- `code/MT5_CODE_PLAN.md`
- `dev/SECURITY_COMPLIANCE_PLAN.md`

## Exit Criteria

- [ ] MT5 Paper chạy ổn định trong môi trường test.
- [ ] Native boundary có readiness review.
