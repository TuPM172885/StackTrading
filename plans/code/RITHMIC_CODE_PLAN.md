# Rithmic Code Plan

## Mục tiêu

Triển khai `Rithmic` adapter theo cùng contract WS2.

## Checklist

- [ ] Tạo project `StackTrading.Infrastructure.Rithmic`.
- [ ] Tạo options/config riêng cho Paper/Live.
- [ ] Implement protobuf-over-TCP transport.
- [ ] Implement session handshake.
- [ ] Implement heartbeat.
- [ ] Implement sequence resync.
- [ ] Model hóa 4 Plants: Order, Tick, History, PnP.
- [ ] Mapping order/account/position events.
- [ ] Implement risk actions.
- [ ] Tạo fake harness hoặc simulator tests.
- [ ] Tạo compatibility tests.

## Exit Criteria

- [ ] Rithmic Paper pass adapter compatibility suite.
- [ ] Reconnect/heartbeat/resync có test.
