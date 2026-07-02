# CI/CD QA Ops Plan

## Mục tiêu

Thiết lập pipeline và kiểm soát chất lượng để WS2 có thể release lặp lại an toàn.

## Checklist

- [ ] CI restore/build/test.
- [ ] Coverage report.
- [ ] Coverage gate `>= 80%`.
- [ ] Risk/money/event path coverage `>= 90%`.
- [ ] Static analysis/analyzers nếu team chốt dùng.
- [ ] Package artifact.
- [ ] Environment config validation.
- [ ] Smoke test sau deploy.
- [ ] Ops dashboard.
- [ ] Alerting policy.

## Exit Criteria

- [ ] Pipeline fail khi test hoặc coverage gate fail.
- [ ] Có artifact deployable cho Host.Service.
