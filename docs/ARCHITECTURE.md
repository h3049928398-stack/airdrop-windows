# 独立工程架构与剩余工作

## 用户决策

- 2026-11-13：软件名 LiquidGlassWinAirDrop，独立开发；DropBridge 仅作原理参考，不复用其代码、补丁、图标和二进制。
- 允许独立的开源底层 USB 驱动/协议库，应用自行开发。
- 使用现有 RTL8811CU USB Wi-Fi 与电脑独立蓝牙，不因名称或设备枚举结果宣称无线可用。
- 之前针对 c811 的驱动切换、初始化、AWDL/BLE 和接收测试已取得用户授权。该授权不意味着可自动关闭系统安全功能或修改别的网卡。旧项目的 Zadig 启动曾被工具拒绝，本工程没有尝试绕过该拒绝。

## 已实现

```text
WPF MainWindow / MainViewModel
 ├─ HardwareInspector → Windows SetupAPI / NetworkInterface（只读）
 ├─ PreferenceStore → atomic JSON replace
 ├─ FolderCatalog → FileSystemWatcher + debounced refresh
 ├─ ReceiveStore → temporary file + byte limit + SHA-256 + atomic publish
 └─ NativeAirDropReceiver → explicit unavailable state（尚未实现）
```

应用构建只依赖 Windows/.NET，本目录外无 ProjectReference、源文件链接、可执行文件启动路径或资源引用。所有 UI 图形由本工程代码/XAML 绘制。`NativeAirDropReceiver` 当前仅是明确的能力边界，不是工作的协议实现。

## 无线整合的实际工作

1. **独立 USB 工作进程**：直接基于独立上游 devourer 与 libusb 构建 C8821C 支持，编写自己的设备选择、只读 open/descriptor、初始化、有限 RX 与退出恢复；禁止调用现有 DropBridge radio。记录 c811 的真实芯片/固件/端点，不能把 c820 的 MI_02 当作本机 c811。
2. **AWDL 工作进程**：评估独立 OWL 的平台依赖，编写自己的 Windows 平台层与帧传输接口。当前上游不是可直接嵌入 WPF 的 Windows 库；没有“加一个引用就能用”的结论。不要复制 DropBridge 的 Windows 补丁。
3. **用户态 IPv6/mDNS/TCP**：在专用无线设备上建立真正的网络路径；Windows 当前普通 Wi-Fi 连接不能直接替代 AWDL。选择独立的网络栈并核查许可。
4. **BLE 辅助发现**：独立实现或采用兼容的独立库，通过 Windows 蓝牙接口发 beacon；先验证蓝牙外设角色，再验证实际广播。普通蓝牙“已检测到”不足以判定可发现。
5. **接收协议**：按独立公开的协议研究实现 TLS、发现、用户确认与归档解析，连接 `ReceiveStore`。建立同一会话的接收许可与配额，不默认接受任意远端上传。
6. **真机验收**：iPhone 分享菜单发现 → 允许接收 → 实际文件落盘 → 大小/哈希核对 → 多次/多文件/中断与恢复。没有这些证据就保持 UI 的“尚未就绪”。

无线整合尚未开始，以上不是已完成的功能清单。界面当前没有虚假的“开启接收”或模拟传输按钮。

## UI 设计

用户否定了第一版的全屏卡片/渐变方向。当前以其草图为准：默认352×480 DIP、最小320×440，干净的竖向内容区，底部四栏导航。默认停靠当前显示器右下工作区，保留12 DIP外侧距离，并留在任务栏上方；可拖动、切换置顶、收起托盘。

`LiquidNavigation` 独立绘制约304×68 DIP的导航材质，按需更新，不进行空闲帧循环。光学输入由 `MainWindow.CaptureBackdrop()` 从应用自己的 `ContentLayer` 采样，排除导航自身避免递归。页面/布局/滚动变化时才重新采样；背景先柔化，之后 `LiquidMaterial` 通过圆角形状的距离与法线计算边缘位移，双线性采样并加入局部色散和高光。选中透镜使用同一背景源独立裁取。文字与原生按钮作为上层保持清晰。

选中位置使用有阻尼弹簧，静止后立即移除 Rendering 事件；关闭动效时取消运动并直接落位。支持拖动透镜及键盘左右/Home/End。滚动边缘做渐隐，减少内容与导航标签相互遮挡。置顶与减少透明度设置可持久化。

本实现是局部折射材质的近似，并非 Apple 私有渲染器。不抓取桌面或其它窗口；在纯白背景上折射本就不明显，不能用夸大的彩色光晕替代实际背景光学。开源方案的查阅与许可边界见 THIRD_PARTY.md。
