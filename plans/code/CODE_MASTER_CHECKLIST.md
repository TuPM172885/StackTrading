# Code Master Checklist

## Mục tiêu

Theo dõi implementation kỹ thuật cho contract, adapter, test và hardening.

## Checklist

- [x] Tạo solution `.NET 8`.
- [x] Tạo slice đầu `TraderEvolution`.
- [x] Có unit/integration baseline.
- [ ] Khóa `IBrokerAdapter` v1.
- [ ] Khóa `BrokerEvent` schema v1.
- [ ] Hoàn thiện `TraderEvolution` production readiness.
- [ ] Tạo `Rithmic` module.
- [ ] Tạo `MT5` module.
- [ ] Tạo contract tests chung.
- [ ] Tạo resilience tests.
- [ ] Tạo coverage gates.

## Exit Criteria

- [ ] Mỗi broker pass cùng một compatibility test suite.
- [ ] Các hot paths có observability và tests tương ứng.
