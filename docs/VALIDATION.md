# 本机验证记录

日期：2026-11-13（Asia/Hong_Kong）。独立工程预览版 0.1。

- .NET SDK 10.0.401、WPF、Windows x64：Release 编译成功，0 warning / 0 error。
- 安全文件存储与偏好设置：13 项验证通过，覆盖真实文件写入和 SHA-256、重名保留、路径穿越/ADS/保留名拒绝、超长/不完整/校验失败/取消后清理、损坏设置保护、真实文件系统监控和搜索。
- 原生 WPF 窗口渲染：接收中心、设备、设置、空文件记录、最小窗口五个视图生成；人工查看接收中心、设备页、设置页、最小窗口，没有发现文字截断或主要控件重叠。设备页底部按预期滚动。
- 本工程自行编写的 SetupAPI 检查成功：RTL8811CU 仍为 `RtlWlanu`、ProblemCode 0，独立 Realtek Bluetooth Adapter 为 `BTHUSB`、ProblemCode 0；内置 RTL8852BE 已连接。只读检测未更换驱动。
- 修正 Windows NetworkInterface 返回底层过滤接口的情况：仅匹配 SetupAPI 所列实际 PCI/USB 网络适配器，避免在设备页展示大量过滤驱动。
- 本机源码未引用工程外的 DropBridge 文件或包。当前无第三方 NuGet 包，构建包源已清空；离线编译无需该仓库。
- 复制本工程的 src、assets 与 NuGet.Config 到独立构建目录后再次编译，0 warning / 0 error；最终发布目录中的自检 13 项通过、硬件诊断 exit 0，仅显示两只实际 Wi-Fi 网卡。

以下均未完成，不能由上述结果替代：USB WinUSB 绑定与回滚、固件初始化、原始无线收发、BLE 实际广播、AWDL/IPv6/mDNS/TLS 端到端互通、iPhone 发现/文件传输、持续负载和其它机器验收。

界面内明确显示原生 AirDrop 接收未就绪。目录与存储测试不是 iPhone 传输测试。
