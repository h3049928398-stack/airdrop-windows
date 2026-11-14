# 当前交接

2026-11-13：用户将软件更名AirDrop，要求移除底部导航点击后的蓝色矩形焦点框，并推进原生隔空投送。两项UI改动已完成。工程目录仍LiquidGlassWinAirDrop，是独立Git工程；不依赖父目录DropBridge代码/补丁/二进制。

已构建接收链：原创WPF监管、devourer USB工作进程、独立WinAWDL/OWL、原创lwIP IPv6/TCP/mDNS桥、独立MIT接收协议与原创逐次确认包装、原创WinRT BLE广播。Windows Job负责子进程退出清理。固定上游版本/工具链SHA256见native.lock.json；恢复/构建/打包脚本在tools。应用入口dist/AirDrop/AirDrop.exe；需要.NET10DesktopRuntime。

17项基础/材质、8项WPF、5项原生协议、84项独立上游Python、6项本地集成检查通过。后者包含合成IPv6握手与双向字节、TLS发现、拒收、允许后32768字节归档落盘/SHA256、Job清理。这不是iPhone实测。

用户已手动完成Zadig绑定。2026-11-13 17:50+08只读确认0BDA:C811为WinUSB/ProblemCode0，libusb probe exit0；内置8852BE仍Up，蓝牙BTHUSB正常。之后的初始化/原始RX/AWDL/手机验收以VALIDATION和WORK_QUEUE的最新补充为准。此前安装器启动被工具拒绝，未绕过。回滚手册和备份见DRIVER_RUNBOOK。

构建：build.ps1 -Preview；原生：tools/Build-Native.ps1；软件检查：AirDrop.exe --engine-check <报告JSON>。普通启动不自动RF，显式--receive或界面开启才启动接收。已完成一次真实iPhone发现ASUS→Windows同意→图片接收，3,401,455字节/4.226秒，图片解码通过。原始日志和本地SHA256见VALIDATION；没有手机源哈希，不声称源/目标哈希已对比。多文件和稳定性未验收，BLE仍Waiting。后续传输仍需用户在手机发起并在Windows确认。没有自动推送或对外发布。

## 2026-11-14目录迁移补充

当前运行入口已改为工作区runtime/AirDrop/AirDrop.exe；源码、.build、upstream、artifacts保留原目录，历史测试记录中的dist/AirDrop路径反映当时位置。原始运行日志、data/keys、设置及既有Received全部迁入新位置，用户自选接收目录不变。

旧参考工程已集中至../archive/DropBridge；旧预览版至../archive/old-previews。使用根Build-AirDrop.ps1构建到新runtime目录，支持-Native/-Preview；源码GUI/Native/Package脚本新增OutputDirectory用于独立指定输出。目录职责、迁移核验和恢复入口见工作区docs/DIRECTORY_LAYOUT.md与REORGANIZATION_2026-11-14.md。
