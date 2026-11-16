# 第三方与参考资料

当前WPF界面为本工程原创，原生接收工作进程已采用下列独立开源组件。没有采用DropBridge的代码、补丁、安装器、图标或二进制。

## 2026-11-13 Windows Liquid Glass 调研

用户明确要求查看 GitHub 参考。GitHub 连接器传输失败后改用公开 README/许可证页面核对，未下载或复制这些项目的代码。

- [shayann07/liquidglass](https://github.com/shayann07/liquidglass)：仓库根许可证为 Apache-2.0，提供 .NET/WPF 等适配。核对 [原生 .NET 文档](https://github.com/shayann07/liquidglass/blob/main/docs/native-dotnet.md)：SkiaSharp 采样绘制、内容/材质层分离、owned-view 快照与按需刷新是可参考的路径。文档同时限定了CPU快照和各平台证据，不能将其作者的测试当作本机证据。当前未采用其包或源文件。
- [crinklecutcarrot/GlassShell](https://github.com/crinklecutcarrot/GlassShell)：README说明WPF、背景采集和自定义HLSL边缘折射，但本次没有找到可确认的根LICENSE，因此只看公开的原理描述，不复制其实现，也不安装其任务栏修改。
- [dragosniamtu/WPF-Liquid-Glass-Effect](https://github.com/dragosniamtu/WPF-Liquid-Glass-Effect)：当前示例依赖 XamlTemplates.FX 专有 Community Binary License；[第三方说明](https://github.com/dragosniamtu/WPF-Liquid-Glass-Effect/blob/main/THIRD-PARTY-NOTICES.md)还明确指出历史着色器授权未获确认。没有因示例仓库标MIT便将整套作为MIT集成。

本版自行实现应用内背景采样、有限面积CPU折射、选中运动与原生按钮，不包含上述项目源代码、第三方着色器或专有运行库。距离场、法线、双线性采样是所使用的通用图形技术；不宣称真实物理模拟或完整复刻苹果材质。

设计参考：

- Apple Materials / Liquid Glass 原理：[Materials](https://developer.apple.com/design/human-interface-guidelines/materials)。参考材质与层次的概念；界面、图标和动效由本工程实现。
- Windows 设备枚举 API：[SetupDiGetClassDevsW](https://learn.microsoft.com/en-us/windows/win32/api/setupapi/nf-setupapi-setupdigetclassdevsw)、[SetupDiGetDeviceRegistryPropertyW](https://learn.microsoft.com/en-us/windows/win32/api/setupapi/nf-setupapi-setupdigetdeviceregistrypropertyw)。


## 已采用的独立底层组件

完整提交见native.lock.json，未修改upstream研究副本。新增适配代码在native和backend；GPL工作进程保持各自许可，应用IPC边界不意味着发布包整体MIT。

| 组件 | 用途 | 许可/本地来源 |
| --- | --- | --- |
| OpenIPC/devourer | C8821C USB无线；链接AirDrop.Radio | GPL-2.0；upstream/devourer/LICENSE |
| raph559/winawdl radio_link.h | 进程间v3无线协议定义 | MIT；root LICENSE |
| WinAWDL awdl + OWL | AWDL协议及Windows平台层 | GPL-3.0-or-later；awdl/LICENSE、OWL/COPYING |
| OWL radiotap | 无线头解析 | 上游独立许可；radiotap/COPYING |
| WinAWDL airdrop模块 | TLS/HTTP收发、DVZip/CPIO/配额/安全落盘 | MIT，Brent Kearney；airdrop/LICENSE。仅打包receiver/transfer/cpio/identity/mdns/netif/redact |
| lwIP 2.2.1 | 用户态IPv6/TCP/UDP | BSD；upstream/lwip/COPYING |
| libusb 1.0.30 | USB设备访问DLL | LGPL-2.1-or-later及上游说明；vcpkg share/libusb/copyright |
| Python 3.12.10 embedded | 本地协议工作进程 | PSF及所含OpenSSL等组件许可；Python/LICENSE.txt |
| LLVM-MinGW 20261104 UCRT | AWDL构建/静态运行库 | 发行包LICENSE.TXT所列许可 |
| Microsoft .NET/WPF/WinForms、VC运行库 | GUI与本机C++运行时 | Microsoft相应运行库许可；非本工程MIT声明范围 |

Package-Engine将上游许可原文、版本锁与本说明放到backend/licenses。完整对应源码在固定上游checkout与本工程native/backend/tools；没有向外发布仅含二进制的包。对外再分发前须补齐GPL/LGPL对应源码义务、Realtek固件再分发依据、微软运行库分发条款和签名，不将本机研究包称为可公开发行产品。没有采用OpenDrop或DropBridge的协议整合代码。

## Wisland连接详情新增依赖（2026-11-16）

独立PyWinRT项目的winrt-Windows.Devices.Enumeration 3.2.1用于读取已配对经典/低功耗蓝牙设备；安装包METADATA License-Expression=MIT，使用原wisland已有winrt-runtime/Bluetooth/Foundation 3.2.1环境，不复制其代码到本扩展。版本固定在integrations/wisland/requirements-extension.txt；项目来源https://github.com/pywinrt/pywinrt。Python、PySide6及wisland原有组件继续使用安装目录既有许可。Wi-Fi ctypes包装为本工程原创，调用Windows自带wlanapi，不新增无线驱动或第三方网络程序。
