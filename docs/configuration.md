# Configuration

## Source-controlled configuration

- `config/defaults/auto_chrome.ini`: cấu hình automation mặc định.
- `config/defaults/auto_chrome_noidung.txt`: nội dung mặc định.
- `config/manager_worker_ipc.json`: thiết lập IPC Manager/Worker.

Khi build Worker, `config/defaults` được copy thành `dist_v13/defaults` để giữ nguyên runtime lookup.

## Runtime data

Profile, tài khoản, logs, queue, backup và thống kê là dữ liệu cục bộ của người dùng. Các thư mục/file này đã được đưa vào `.gitignore` và không được di chuyển nếu không có migration/fallback.

## Test fixtures

Các dữ liệu IPC kiểm thử cũ được bảo toàn tại `tests/fixtures/legacy-ipc`; chúng không còn nằm ở root và không được dùng làm dữ liệu production.
