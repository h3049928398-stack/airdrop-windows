# LiquidGlassWinAirDrop

独立开发的 Windows 原生桌面应用，名称为 **LiquidGlassWinAirDrop**。当前界面按用户草图改为约 **352×480 DIP** 的竖向小浮窗，默认贴近工作区右下角；内容区保持干净，底部为「接收／文件／连接／设置」四栏。

液态玻璃效果集中于底部导航：采样本窗口内的内容，柔化背景后进行边缘折射、细微色散、亮边和选中透镜的弹簧位移。支持点击、拖动透镜、键盘左右切换。使用独立编写的小面积 CPU 采样器，不录制桌面、不安装系统主题，不是苹果官方渲染器。

## 当前版本的能力边界

**当前是可运行的独立开发预览版，尚不能接收 iPhone 原生隔空投送。** 接收中心、文件目录与记录、硬件检查、设置持久化及安全存储已经实现；原生 USB 无线后端、AWDL、IPv6/mDNS 与 AirDrop 协议仍需开发整合。

不会把界面渲染、USB 枚举、目录文件或本地存储测试描述成 AirDrop 已成功。

## 运行

双击 `dist/LiquidGlassWinAirDrop/LiquidGlassWinAirDrop.exe`。当前机器已安装所需 .NET 10 Desktop Runtime；本版为依赖运行时的文件夹发行版，需要保留同目录的 DLL 与 JSON 文件。

- 接收：显示当前接收能力与设备名称，可运行只读连接检查。
- 文件记录：列出所选接收目录中的文件，支持名称搜索；双击只在资源管理器中定位，不自动执行文件。
- 设备与连接：通过 Windows SetupAPI 只读检查 USB Wi-Fi、独立蓝牙、原驱动和网络状态，不修改驱动或网络。
- 设置：修改设备名、接收目录、导航动画、减少透明度与置顶；点击保存后写入程序旁 `data/preferences.json`。顶部图钉也可切换并保存置顶状态。
- 顶部空白区域可拖动。关闭或最小化按钮收起到托盘；双击托盘图标还原，托盘菜单可重新停靠右下角或退出。停靠使用当前显示器的工作区，避开任务栏。

文件记录只代表接收目录的实际内容，**不把用户自行放入的文件标记为 AirDrop 传输**。`Received` 目录中的文件目前仍需要由用户或后续接收引擎写入。

## 编译与验证

要求 Windows x64、.NET 10 SDK 和 Desktop targeting pack。当前应用不使用第三方 NuGet 包，`NuGet.Config` 清空外部包源，可离线构建。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Preview
```

结果：`dist/LiquidGlassWinAirDrop`；验证报告：`artifacts/self-test.json`；五张原生 WPF 视图渲染位于 `artifacts/`。`material-proof.png` 为合成条纹的折射诊断样本，`material-check.json` 记录背景像素变化与局部透镜采样耗时，它们不是生产界面或 AirDrop 传输证据。

```powershell
.\dist\LiquidGlassWinAirDrop\LiquidGlassWinAirDrop.exe --diagnose .\artifacts\hardware.local.json
```

该命令只读取设备状态，报告可能包含硬件标识，仅保存在本地、不纳入提交。

## 独立性与后续无线实现

本工程没有引用、复制、启动或打包 DropBridge 的源代码、补丁、图标、安装器或发布二进制。此前下载的项目保留在本工程外，不是运行或构建依赖。即使将本目录单独复制出去，也可以构建运行。

用户允许采用独立的开源底层库。候选为 OpenIPC/devourer（USB 原始无线层）、seemoo-lab/OWL（AWDL 协议）、OpenDrop（AirDrop 协议资料/可能的独立工作进程）。这些尚未引入当前构建。引入前固定版本、核查各自许可，并保持应用与不同 GPL 工作进程的边界；不复用 DropBridge 的整合代码。

详见 [架构与下一阶段](docs/ARCHITECTURE.md)、[验证记录](docs/VALIDATION.md)、[第三方与参考资料](docs/THIRD_PARTY.md)。
