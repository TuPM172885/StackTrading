# Plans

Thư mục này là nơi theo dõi kế hoạch triển khai `WS2` cho StackTrading.

## Cách dùng

- `MASTER_PLAN.md` là roadmap tổng thể.
- `STATUS.md` là bảng trạng thái ngắn để xem nhanh trước mỗi buổi làm việc.
- `DECISIONS.md` ghi lại quyết định quan trọng về kiến trúc, delivery và vận hành.
- `RISKS_AND_DEPENDENCIES.md` ghi blocker, dependency ngoài và rủi ro.
- `GLOSSARY.md` chuẩn hóa thuật ngữ dự án.
- `dev/` dùng cho delivery, QA, release, ops và readiness.
- `code/` dùng cho implementation kỹ thuật, contract, adapter, test và hardening.

## Quy ước checklist

- `[ ]` chưa làm.
- `[x]` hoàn tất.
- `[-]` hoãn hoặc không áp dụng có chủ đích.

Mỗi checklist nên có `Exit Criteria`. Khi task phát sinh, thêm vào file plan đúng ownership thay vì ghi rải rác trong README hoặc chat.

## Quy ước cập nhật

- Cập nhật `STATUS.md` khi đổi ưu tiên, có blocker mới hoặc hoàn thành một epic.
- Ghi quyết định lớn vào `DECISIONS.md` trước khi triển khai rộng.
- Ghi dependency ngoài vào `RISKS_AND_DEPENDENCIES.md` ngay khi phát hiện.
- Không đưa secrets thật vào bất kỳ file plan nào.
