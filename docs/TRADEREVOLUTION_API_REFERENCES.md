# TraderEvolution API References

File này lưu nguồn tham chiếu chính thức để dùng khi tích hợp `TraderEvolution` adapter.

## Client API

- Authorization process: https://guide.traderevolution.com/traderevolution-api/traderevolution-apis/client-api/authorization-process
- Client API overview: https://guide.traderevolution.com/traderevolution-api/traderevolution-apis/client-api
- Account management: https://guide.traderevolution.com/traderevolution-api/traderevolution-apis/client-api/account-management
- Trading: https://guide.traderevolution.com/traderevolution-api/traderevolution-apis/client-api/trading
- Subscription to trading data: https://guide.traderevolution.com/traderevolution-api/traderevolution-apis/client-api/subscription-to-trading-data
- Subscription to account details: https://guide.traderevolution.com/traderevolution-api/traderevolution-apis/client-api/subscription-to-account-details

## Sandbox Swagger

- Swagger UI: https://sandbox-api.traderevolution.com/traderevolution/v1/swagger-ui/#/
- Account methods: https://sandbox-api.traderevolution.com/traderevolution/v1/swagger-ui/#/Account

## Integration Notes

- Base REST path trong tài liệu là `/traderevolution/v1`.
- REST API dùng `Authorization: Bearer <access_token>`.
- OAuth token endpoint sandbox: `https://sandbox-api.traderevolution.com/traderevolution/v1/oauth/token`.
- OAuth authorize endpoint sandbox: `https://sandbox-api.traderevolution.com/traderevolution/v1/oauth/authorize`.
- WebSocket cũng dùng Bearer token trong `Authorization` header hoặc query `access_token`.
- Trading event stream mặc định: `/traderevolution/v1/stream/tradeEvents`.
- Account detail stream mặc định: `/traderevolution/v1/stream/accounts`.

