# 第三方与参考资料

当前编译输出没有打包第三方应用或第三方协议代码；依赖 Microsoft .NET/WPF/WinForms 和 Windows 系统 API。没有采用 DropBridge 的代码或二进制，也没有使用其图标、资源或补丁。

## 2026-11-13 Windows Liquid Glass 调研

用户明确要求查看 GitHub 参考。GitHub 连接器传输失败后改用公开 README/许可证页面核对，未下载或复制这些项目的代码。

- [shayann07/liquidglass](https://github.com/shayann07/liquidglass)：仓库根许可证为 Apache-2.0，提供 .NET/WPF 等适配。核对 [原生 .NET 文档](https://github.com/shayann07/liquidglass/blob/main/docs/native-dotnet.md)：SkiaSharp 采样绘制、内容/材质层分离、owned-view 快照与按需刷新是可参考的路径。文档同时限定了CPU快照和各平台证据，不能将其作者的测试当作本机证据。当前未采用其包或源文件。
- [crinklecutcarrot/GlassShell](https://github.com/crinklecutcarrot/GlassShell)：README说明WPF、背景采集和自定义HLSL边缘折射，但本次没有找到可确认的根LICENSE，因此只看公开的原理描述，不复制其实现，也不安装其任务栏修改。
- [dragosniamtu/WPF-Liquid-Glass-Effect](https://github.com/dragosniamtu/WPF-Liquid-Glass-Effect)：当前示例依赖 XamlTemplates.FX 专有 Community Binary License；[第三方说明](https://github.com/dragosniamtu/WPF-Liquid-Glass-Effect/blob/main/THIRD-PARTY-NOTICES.md)还明确指出历史着色器授权未获确认。没有因示例仓库标MIT便将整套作为MIT集成。

本版自行实现应用内背景采样、有限面积CPU折射、选中运动与原生按钮，不包含上述项目源代码、第三方着色器或专有运行库。距离场、法线、双线性采样是所使用的通用图形技术；不宣称真实物理模拟或完整复刻苹果材质。

设计参考：

- Apple Materials / Liquid Glass 原理：[Materials](https://developer.apple.com/design/human-interface-guidelines/materials)。参考材质与层次的概念；界面、图标和动效由本工程实现。
- Windows 设备枚举 API：[SetupDiGetClassDevsW](https://learn.microsoft.com/en-us/windows/win32/api/setupapi/nf-setupapi-setupdigetclassdevsw)、[SetupDiGetDeviceRegistryPropertyW](https://learn.microsoft.com/en-us/windows/win32/api/setupapi/nf-setupapi-setupdigetdeviceregistrypropertyw)。

候选独立底层项目（尚未引入、未构建、未集成）：

| 项目 | 角色 | 引入前要求 |
| --- | --- | --- |
| [OpenIPC/devourer](https://github.com/OpenIPC/devourer) | 用户态原始 Wi-Fi/RTL8811CU 驱动路线 | 固定提交，核查代码与固件许可，Windows/C8821C 编译与本机验证 |
| [seemoo-lab/OWL](https://github.com/seemoo-lab/owl) | AWDL 协议 | 固定提交、逐组件许可核查、自行实现 Windows 平台层 |
| [seemoo-lab/OpenDrop](https://github.com/seemoo-lab/opendrop) | AirDrop 协议研究与接收实现 | 固定提交、许可核查、独立进程/自研实现边界、iPhone 兼容验证 |

没有把库的存在当成 Windows 原生 AirDrop 已经可用的证据。
