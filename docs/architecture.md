# Architecture

## Dependency graph

```text
ToolTikTok.Manager
  ├─ ToolTikTok.Infrastructure
  ├─ ToolTikTok.Automation
  ├─ ToolTikTok.UI
  ├─ ToolTikTok.Contracts
  └─ ToolTikTok.Worker (build-order only)

ToolTikTok.Worker
  ├─ ToolTikTok.Automation
  ├─ ToolTikTok.UI
  └─ ToolTikTok.Contracts

ToolTikTok.Infrastructure
  ├─ ToolTikTok.Automation
  └─ ToolTikTok.Contracts

ToolTikTok.Automation
  ├─ ToolTikTok.UI
  └─ ToolTikTok.Contracts
```

`Contracts` không phụ thuộc UI/Manager/Worker. Không có circular project reference.

## Compatibility boundaries

- Assembly/executable names vẫn là `ToolTikTokManagerV13` và `ToolTikTokWorkerV13`.
- Namespace legacy được giữ để tránh thay đổi reflection và serialized identifiers.
- Pipe prefix và handshake version không đổi.
- Output runtime vẫn là `dist_v13`.
- Defaults vẫn được copy thành thư mục `defaults` cạnh executable.

## God-class inventory

| File | Lines (inventory) | Nhóm trách nhiệm | Hướng extraction an toàn |
|---|---:|---|---|
| `ManagerForm.cs` | 5,058 | UI shell, Worker lifecycle, profile commands | WorkerManager, RuntimeStateService |
| `ChromeController.cs` | 4,144 | Chrome/CDP, navigation, identity | ChromeSession, CdpNavigationService |
| `TikTokAccountPoolService.cs` | 3,903 | parsing, persistence, allocation | AccountPoolRepository, AccountAllocator |
| `MainForm.cs` | 2,349 | Worker UI và orchestration | WorkerViewModel, WorkerLifecycle |
| `AutomationEngine.cs` | 2,291 | live loop, viewer, input guard | ViewerMonitor, LiveSwitchService |
| `ManagerForm.DashboardUpdate.cs` | 2,161 | update UI và updater flow | UpdateCoordinator |

Task cấu trúc này không rewrite các class trên. Extraction tiếp theo phải làm từng phần nhỏ và có test.
