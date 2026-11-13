# 当前进度与交接

2026-11-13：用户将原任务改为独立开发 LiquidGlassWinAirDrop，要求液态玻璃外观，允许采用独立开源底层库。旧项目代码/二进制不作依赖。

已交付本目录内的原创 WPF 应用基础版，可独立编译运行，具有四个页面、玻璃风格与指针光影、设置、目录监控与检索、只读硬件检测、通知区域入口、原子安全存储和测试。

**整个原生 AirDrop 目标仍未完成。** 未实现 USB 无线工作进程、AWDL/IPv6、BLE 广播及端到端接收协议，不能用 GUI 完成或本地存储测试替代。源码里的 NativeAirDropReceiver 明确返回不可用。下一阶段按 ARCHITECTURE.md 的无线整合工作推进，不要重新使用上级 DropBridge 的 exe 或补丁来填补空缺。

用户此前已授权仅针对现有 `0bda:c811` 的驱动切换、初始化、AWDL/BLE 和 iPhone 接收测试。再次检查其驱动仍为 `RtlWlanu`，内置 RTL8852BE 上网、独立蓝牙正常。当前没有切换驱动。旧 Zadig 启动曾被电脑控制工具拒绝；不要换执行途径规避该拒绝。

构建：`powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1 -Preview`。最终程序：`dist/LiquidGlassWinAirDrop/LiquidGlassWinAirDrop.exe`。需要 .NET 10 Desktop Runtime（本机已有），非自包含发布。测试、诊断和截图在 artifacts，诊断 JSON 不入库。父目录保留旧研究材料，本应用不引用它。
