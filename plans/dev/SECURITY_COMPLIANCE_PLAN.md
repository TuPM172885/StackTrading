# Security Compliance Plan

## Mục tiêu

Đảm bảo WS2 đáp ứng yêu cầu bảo mật, audit và compliance trước khi chạm Live.

## Checklist

- [ ] Secrets dùng `AWS Secrets Manager`.
- [ ] Không commit secrets thật.
- [ ] IAM least privilege.
- [ ] Audit log cho risk actions.
- [ ] Trace `correlationId` xuyên command/event.
- [ ] Paper/Live guard có test.
- [ ] Live readiness gate.
- [ ] Secrets rotation plan.
- [ ] Dependency/security scanning.
- [ ] Compliance review cho broker Live.

## Exit Criteria

- [ ] Không bật Live nếu thiếu audit, secrets và Paper/Live guard.
