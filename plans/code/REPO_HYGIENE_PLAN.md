# Repo Hygiene Plan

## Mục tiêu

Giữ repo sạch, dễ build, dễ review và không lẫn artifact/generated files.

## Checklist

- [x] Tạo `.gitignore` cho `.NET`, `bin/`, `obj/`, test results, IDE files.
- [ ] Kiểm tra `bin/obj` không được đưa vào source control nếu repo Git được khởi tạo.
- [ ] Chuẩn hóa naming `TraderEvolution`.
- [ ] Không dùng tên `TraderRevolution` trong code/spec.
- [ ] Kiểm tra encoding Markdown tiếng Việt.
- [ ] Chuẩn hóa line endings nếu cần.
- [ ] Thêm `.editorconfig` nếu team muốn enforce formatting.
- [ ] Xem xét bật nullable/analyzers nghiêm hơn sau khi foundation ổn định.

## Exit Criteria

- [ ] Repo clone mới có thể restore/build/test không cần artifact cũ.
- [ ] Không có secrets hoặc generated build outputs trong source tracking.
