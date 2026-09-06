# UI audit — TikTok Operations Center

Ngày audit: 2026-09-06  
Phạm vi: `src/ToolTikTok.Manager`, `src/ToolTikTok.Automation`, `src/ToolTikTok.UI`.

## 1. Tóm tắt hiện trạng

Ứng dụng đã có một lớp giao diện dùng chung (`UiTheme`, `ModernDialog`,
`ModernProgressDialog`) và màn hình Quản lý hồ sơ đã tiến gần ngôn ngữ thiết kế
mới. Tuy nhiên, UI vẫn được dựng trực tiếp trong nhiều partial class rất lớn.
Nhiều tác vụ dài hạn được mở bằng modal, điều hướng sidebar đang gắn trạng thái
active cố định, và token màu/font/khoảng cách chưa được tập trung đầy đủ.

Không cần viết lại ứng dụng. Hướng an toàn là giữ nguyên service, handler và luồng
IPC; tách dần phần trình bày thành workspace/page và component dùng chung.

## 2. Inventory bề mặt UI

| Bề mặt | Entry point chính | Hiện trạng | Hướng xử lý |
|---|---|---|---|
| Shell Manager | `ManagerForm.BuildLayout` | Sidebar 272 px, active cố định, header lặp version/update | Chuẩn hóa 236 px, active động, header theo page |
| Tổng quan | `InitializeDashboardAndUpdater`, `BuildDashboardStatistics` | Có KPI/update/profile card; trách nhiệm hơi trộn | Giữ dashboard là overview, bỏ bảng profile trùng lặp |
| Quản lý hồ sơ | `ShowProfileManagementPage` | Workspace tốt nhất hiện tại | Chọn làm golden screen; command bar + overflow |
| Thêm/sửa hồ sơ | `AddProfile`, `EditProfile` và `ModernDialog` | Transactional modal phù hợp | Chọn làm golden modal, chuẩn hóa spacing/validation |
| Cập nhật danh tính TikTok | `ShowTikTokIdentityDialog` (1.064 dòng) | Modal lớn, có preview và tiến trình | Chuyển thành Profile sub-workspace |
| Tạo hồ sơ tự động | `ShowAutoProfileDialog` (1.467 dòng) | Tác vụ dài hạn trong modal | Chuyển thành Profile sub-workspace |
| Tin nhắn | `ShowTikTokMessageReplyDialog` (1.496 dòng) | Modal lớn, manual/auto/journal | Chuyển thành page với Manual / Tự động / Trạng thái |
| Tài khoản | `ShowAccountPoolDialog` trong `ManagerForm.cs` | Modal quản lý dữ liệu | Chuyển thành page Dữ liệu > Tài khoản |
| Nhật ký tự động | `ShowAutoActivityLogDialog` (776 dòng) | Modal tra cứu | Chuyển thành page Hệ thống > Nhật ký |
| Thiết lập | `ShowDefaultConfigDialog`, `ShowUpdateSettingsDialog` | Nhiều dialog cấu hình | Page Thiết lập; modal chỉ cho xác nhận/chọn file |
| Giám sát Chrome | `ChromeMonitorForm` (553 dòng) | Form độc lập đúng mục đích | Giữ độc lập, chỉ áp theme và component chung |
| Worker Automation | `Automation.MainForm.BuildUi` (2.376 dòng) | Tab Vận hành/Người xem/Chẩn đoán, nhiều UI inline | Giữ luồng, chuẩn hóa token, tabs và trạng thái |
| Worker managed host | `WorkerHost/MainForm.*` | Bề mặt nhúng/managed | Không đổi giao thức; chỉ đồng bộ visual khi cần |

## 3. Các dialog/form lớn cần giảm dần

| File | Quy mô gần đúng | Vấn đề |
|---|---:|---|
| `ManagerForm.cs` | 5.116 dòng | Shell, profile lifecycle và nhiều dialog cùng một file |
| `ManagerForm.AutoClose.cs` | 1.495 dòng | UI cấu hình và logic vận hành gắn chặt |
| `ManagerForm.MessageReply.cs` | 1.496 dòng | Modal dài hạn, nhiều trạng thái cục bộ |
| `ManagerForm.AutoProfile.cs` | 1.467 dòng | Workflow dài hạn trong modal |
| `ManagerForm.DashboardUpdate.cs` | 2.161 dòng | Dashboard và updater dùng chung một vùng trách nhiệm |
| `ManagerForm.IdentityUpdate.cs` | 1.064 dòng | Modal công cụ lớn |
| `ManagerForm.AutoReplacement.cs` | 1.111 dòng | UI và scheduler cùng partial |
| `ManagerForm.ReusableProfileQueue.cs` | 1.257 dòng | Grid/queue và orchestration gắn chặt |

## 4. Design-system gaps

- Màu cơ bản đã có trong `UiTheme`, nhưng tint, hover, divider, surface và trạng
  thái vẫn xuất hiện dưới dạng `Color.FromArgb(...)` rải rác.
- `new Font(...)` được tạo tại nhiều feature, làm typography khó đồng nhất.
- Spacing, chiều cao control, radius, sidebar/header chưa có token chung.
- Glyph Unicode được gõ trực tiếp tại nhiều nơi; font và ý nghĩa không nhất quán.
- KPI card, page header, status badge, empty state và command bar đang được dựng
  lại theo từng màn hình.
- `UiTheme.Apply` duyệt đệ quy nhưng chỉ theme một phần control; style cục bộ có
  thể xung đột với style chung.

## 5. Information architecture mục tiêu

```text
Tổng quan
└─ Tổng quan

Vận hành
├─ Hồ sơ
├─ Tin nhắn
└─ Giám sát Chrome

Dữ liệu
└─ Tài khoản

Hệ thống
├─ Nhật ký
└─ Thiết lập
```

Các thao tác `Tạo hồ sơ tự động`, `Cập nhật TikTok`, `Đồng bộ Chrome`, `Đổi tên`
và `Xóa` thuộc ngữ cảnh Hồ sơ, không còn là mục sidebar cấp cao.

## 6. Quy tắc bảo toàn hành vi

- Giữ nguyên `ProfileContext`, profile catalog, account pool và các service hiện có.
- Giữ nguyên tên/luồng handler nghiệp vụ; page mới chỉ gọi lại handler hiện hữu.
- Không thay đổi named pipe, CDP, worker startup/shutdown hoặc schema lưu trữ.
- Không đổi thứ tự shutdown/cleanup và không chuyển tác vụ nền sang UI thread.
- Mỗi lần migrate một bề mặt phải có đường quay về handler cũ cho tới khi page
  mới đã bao phủ đủ chức năng.

## 7. Kế hoạch triển khai theo batch

1. **Foundation:** token màu/font/spacing/metrics/icon và component cơ sở.
2. **Shell:** sidebar IA mới, active state động, page-aware header.
3. **Golden screen:** hoàn thiện Quản lý hồ sơ, contextual command bar, trạng thái
   empty/loading/error/no-results.
4. **Golden modal:** chuẩn hóa Add/Edit profile và validation.
5. **Dashboard:** overview, KPI và recent activity; không lặp profile grid.
6. **Messaging:** migrate modal thành workspace nhiều tab.
7. **Identity/Auto Profile:** migrate thành Profile sub-workspace dài hạn.
8. **Account/Logs/Settings:** migrate thành các page cấp cao.
9. **Automation/Monitor:** đồng bộ token và visual, không đổi logic.
10. **Responsive/accessibility:** DPI, keyboard, focus, contrast và các size mục tiêu.
11. **Validation/report:** build một lượt, snapshot có timeout hữu hạn, ghi báo cáo.

## 8. Tiêu chí hoàn thành

- Sidebar đúng IA và active state phản ánh page đang chọn.
- Mọi page có header, command hierarchy và state rõ ràng.
- Không còn màn hình quản lý dài hạn bắt buộc chạy trong modal.
- Dialog chỉ dùng cho tác vụ ngắn/transactional.
- Các control cốt lõi dùng token/component chung; không thêm glyph tùy tiện.
- Hoạt động ở 1280×720, 1366×768, 1600×900 và DPI phổ biến.
- Build Release pass; snapshot harness kết thúc trong tối đa 60 giây.
