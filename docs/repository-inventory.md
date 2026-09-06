# Repository refactor inventory

## Before

Toàn bộ source, scripts, installer, defaults, runtime scratch data và build output nằm dưới `TOOL-V13-main/`. Solution chỉ có Manager/Worker và dùng nhiều `Compile Include` để link source.

## Runtime/generated classification

- Runtime/user data: profiles, logs, queues, backups, statistics.
- Test scratch: `ipc_restart_*`, `ipc_test_*`.
- Build output: `.vs`, `bin`, `obj`, `dist_*`, `build_*`, `publish_*`.
- Release output: `SETUP_OUTPUT`, `RELEASE_OUTPUT`, `SOURCE_OUTPUT`, ZIP/PDB.

## After

Source ở `src/`, tests/fixtures ở `tests/`, scripts ở `scripts/`, installer ở `installer/windows`, defaults ở `config/defaults`, tài liệu ở `docs/`. Root chỉ giữ solution và metadata cấp repository.

## High-risk work intentionally deferred

- Đổi namespace `ToolTikTokV11`/`ToolTikTokV12`.
- Đổi AssemblyName hoặc tên executable có version.
- Rewrite ManagerForm, ChromeController, AutomationEngine.
- Đổi IPC payload/prefix hoặc runtime data location.
- Chạy release/upload thật.
