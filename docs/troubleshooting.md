# Troubleshooting

## Build bị khóa file

Đóng Manager/Worker đang chạy rồi build lại. Hai process dùng file trong `dist_v13`.

## Worker không mở Named Pipe

Kiểm tra `logs/worker-bootstrap.log`, pipe name trong tham số Worker và `config/manager_worker_ipc.json`. Không đổi prefix `ToolTikTokV13_` khi chưa nâng protocol.

## Chrome/CDP không kết nối

Kiểm tra Chrome path, CDP port của profile và không chạy hai Worker dùng cùng profile/port.

## Defaults không xuất hiện

Build từ `ToolTikTok.sln` hoặc `scripts/build.ps1`, sau đó xác nhận `dist_v13/defaults` tồn tại.

## Restore NuGet thất bại

Kiểm tra kết nối tới `https://api.nuget.org/v3/index.json`, sau đó chạy lại `dotnet restore ToolTikTok.sln`.
