# TraderEvolution Code Plan

## Mục tiêu

Hoàn thiện `TraderEvolution` adapter từ slice đầu lên production-ready.

## Checklist

- [x] Có REST client skeleton.
- [x] Có WebSocket stream skeleton.
- [x] Có Paper/Live options và validation cơ bản.
- [x] Có fake broker integration test.
- [ ] Thay fake DTO bằng DTO mapping theo broker docs thật.
- [ ] Hoàn thiện auth flow thật.
- [x] Hoàn thiện order status mapping.
- [x] Hoàn thiện account state mapping.
- [x] Hoàn thiện position mapping, gồm FX swap nếu có.
- [x] Hoàn thiện `FlattenAllAsync` cho nhiều position bằng reduce/close order flow.
- [ ] Hoàn thiện `FlattenAllAsync` cho pending order khi có broker order-list/cancel spec.
- [x] Hoàn thiện `TrimToComplianceAsync` theo reduce strategy.
- [x] Thêm event normalization tests.
- [ ] Thêm reconnect/resubscribe tests.
- [x] Thêm broker error translation tests.

## Exit Criteria

- [ ] Paper sandbox thật pass create/order/query/stream/risk.
- [ ] Event publish đủ header và payload cho downstream.
- [ ] Risk actions deterministic và audit được.
