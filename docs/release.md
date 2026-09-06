# Release

## Single source of truth

Chỉ sửa `VERSION.txt`. `Directory.Build.props` truyền giá trị này vào Version, FileVersion, AssemblyVersion và InformationalVersion.

## Local flow

1. Chạy tests.
2. Publish Worker và Manager vào `publish_v13_5_vm`.
3. Kiểm tra đủ hai executable và defaults.
4. Build Inno Setup nếu máy có ISCC.
5. Tính SHA256 và đồng bộ `version.json`/`versions.json` bằng `scripts/SYNC_VERSION.ps1`.

`scripts/release.ps1` điều phối các bước local nhưng không upload hoặc tạo GitHub release.

Tên executable được giữ nguyên vì Manager discovery, installer và updater đang phụ thuộc vào chúng.
