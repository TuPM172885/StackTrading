# StackTrading WS2

Adapter `TraderEvolution` đầu tiên cho `WS2`, triển khai theo mô hình `standalone service`.

## Thành phần chính

- `src/StackTrading.Contracts`: contract, model, `IBrokerAdapter`
- `src/StackTrading.Application`: orchestration, dedupe, subscription registry
- `src/StackTrading.Infrastructure.TraderEvolution`: transport `REST + WebSocket`, config và runtime guard
- `src/StackTrading.Host.Service`: HTTP API, background stream worker, Kafka publisher
- `tests`: unit test và integration test với fake broker

## Chạy local

```bash
dotnet test StackTrading.slnx
dotnet run --project src/StackTrading.Host.Service
```

## Tài liệu

- [Hướng dẫn sử dụng](docs/USER_GUIDE.md)
