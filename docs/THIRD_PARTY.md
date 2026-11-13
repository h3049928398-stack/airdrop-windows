# 第三方与参考资料

当前编译输出没有打包第三方应用或第三方协议代码；依赖 Microsoft .NET/WPF/WinForms 和 Windows 系统 API。没有采用 DropBridge 的代码或二进制，也没有使用其图标、资源或补丁。

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
