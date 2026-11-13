# AirDrop

独立开发的 Windows x64 接收应用，原工程名 LiquidGlassWinAirDrop。默认约352×480 DIP，停靠屏幕右下工作区；底部「接收／文件／连接／设置」采用自行实现的应用内 Liquid Glass 近似材质。底部按钮点击后不再显示蓝色矩形焦点框，键盘导航仍可用。

**已在本机完成一次真实 iPhone 原生隔空投送：手机发现 ASUS → Windows 确认 → 图片保存成功。** 2026-11-13实收3,401,455字节，接收开始至落盘4.226秒，约0.805 MB/s（包含协议收尾等待）；文件大小与发送声明一致，图片解码通过。本机USB网卡由用户绑定WinUSB。单次成功不代表所有iOS版本、多文件或长期稳定性通过；BLE广播仍未观察到Started。详见验证记录。

## 运行

双击 `dist/AirDrop/AirDrop.exe`，保留同目录文件；需要 .NET 10 Desktop Runtime。界面支持设备名、接收目录、文件搜索、动画/透明度、置顶和收起托盘。文件页列出真实目录内容，不将手动放入的文件冒称 AirDrop 传输。

WinUSB 准备完成后，手动点击「开启接收」才启动专用 USB、AWDL、用户态 IPv6/TCP、TLS 和 BLE 发现组件。每次请求需明确确认，45秒无答复默认拒绝；文件默认存入设置的目录。关闭窗口收起托盘，托盘「退出」停止后台；Windows Job 负责应用意外退出时清理其子进程。

仅实现接收方向，不承诺向 iPhone 发送、Apple 身份认证或所有 iOS 版本兼容。当前不能将其当作已可用的正式产品。

## 构建

本目录是独立 Git 工程，不引用父目录 DropBridge 的源代码、补丁、资源、安装包或二进制。允许的独立开源底层库版本见 `docs/native.lock.json`。

```powershell
.\build.ps1 -Preview
.\tools\Restore-Sources.ps1 -WithBuildDependencies
.\tools\Build-Native.ps1
.\dist\AirDrop\AirDrop.exe --engine-check .\artifacts\engine-check.json
```

GUI要求.NET 10 SDK；原生层要求 Visual Studio C++/CMake/Ninja、Git。恢复脚本将依赖下载到本目录并核对固定提交、工具链归档SHA-256，不安装系统驱动。协议工作进程使用固定 LLVM-MinGW、libusb、Python 3.12.10；没有 WSL、虚拟机、Wintun 或 iPhone 端应用依赖。原生构建脚本已经在本机执行；从空白机器完整下载重建尚未验收。

只读诊断：`AirDrop.exe --diagnose <输出JSON>`；`backend/AirDrop.Radio.exe --probe`只尝试打开USB和读取描述符，不初始化射频。不要把 `--run`、`--listen` 或 beacon.ps1 当作只读命令。

## 数据与交接

`data/preferences.json`保存设置，`data/keys/`保存自签TLS私钥与稳定服务ID，`data/logs/engine.log`保存带时间的后台日志；私钥和本机标识不纳入Git。接收引擎仅在127.0.0.1:18771提供TLS，AWDL流量经用户态网络桥转入，不创建系统网络适配器或更改防火墙。

驱动操作见 [本机绑定与恢复](docs/DRIVER_RUNBOOK.md)。准确进度见 [工作队列](docs/WORK_QUEUE.md)、[验证记录](docs/VALIDATION.md)、[第三方许可](docs/THIRD_PARTY.md)。本包仅用于本机研究；固件与运行库再分发、代码签名、真实设备与长期稳定性仍需单独处理。
