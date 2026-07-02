# MT5 Code Plan

## Mục tiêu

Triển khai `MT5` adapter với native boundary an toàn.

## Checklist

- [ ] Tạo project `StackTrading.Infrastructure.MT5`.
- [ ] Thiết kế native wrapper boundary.
- [ ] Kiểm soát lifetime của native handles.
- [ ] Implement Manager API auth/session.
- [ ] Implement account/user operations.
- [ ] Implement order/position/state.
- [ ] Implement manager sinks/callbacks.
- [ ] Implement symbol mapping.
- [ ] Implement swap sync nếu thuộc scope adapter.
- [ ] Implement failover primary/backup.
- [ ] Tạo crash recovery tests.
- [ ] Tạo memory-safety review checklist.

## Exit Criteria

- [ ] MT5 Paper pass adapter compatibility suite.
- [ ] Native wrapper có isolation/recovery strategy rõ ràng.
