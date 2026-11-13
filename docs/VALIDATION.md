# 本机验证记录

## 2026-11-13：AirDrop接收引擎与改名

- UI/基础：Release构建成功，17项基础/材质和8项WPF生命周期通过；新截图显示AirDrop名称和无矩形焦点框的底部按钮。
- 独立上游版本见native.lock.json。devourer C8821C、WinAWDL/OWL和原创lwIP桥均已在Windows x64编译。Build-Native脚本本机执行通过；干净机器完整依赖恢复尚未做。
- 本地运行5个独立上游原生测试程序通过：frame_fixup、host_frame、redact、service_bridge、tcp_merge。LLVM下service_bridge原测试将20字节DNS后缀长度写成21，越过字符串末尾；CMake只在构建目录把测试长度改为sizeof(FULL_TYPE)-1，生产协议源码未因此修改。原始失败日志保留。
- 独立WinAWDL Python测试本机84项全部通过（24.606秒），不是作者报告。沙箱内本地socket被拒绝的失败日志也保留；扩展执行权限后测试通过。
- 集成6项通过：合成IPv6/TCP握手；管道/lwIP/loopback双向字节；TLS Discover二进制plist；用户拒绝返回401且不新增文件；用户允许后32768字节合成gzip/CPIO归档经TLS落盘、发文件事件、SHA256一致；关闭Windows Job终止自有工作进程。
- 修复实测暴露的问题：MSVC结构体打包、64位IPv6分片头配置，以及同步管道ReadFile阻塞WriteFile。改用可取消OVERLAPPED读写；失败日志保留在artifacts及dist/AirDrop/*check*.log。
- BLE只做过ValidateOnly构造/能力检查；当时没有广播。不能据此声称手机已发现。
- 硬件初始probe exit2，普通驱动RtlWlanu；用户手动完成WinUSB绑定后，17:50+08硬件检查c811 WinUSB、ProblemCode0，probe exit0。内置8852BE仍Up，独立蓝牙BTHUSB、ProblemCode0。

证据本地：artifacts/self-test.json、ui-check.json、engine-check.json、protocol-tests-native.log、protocol-tests-native-stderr.log、native-build-script.log、hardware.after-binding.local.json、usb-after-binding.log。报告不提交硬件标识、私钥或原始无线帧。源码/上游报告/本机编译/本机硬件/真实手机文件分开记录。

## 2026-11-13：0.2 小浮窗与底部液态导航

- 发布构建成功。17项基础/材质检查全部通过：原13项加圆角透明边界、背景位移确实改变像素、中心保持原始采样、背景柔化四项。
- 合成条纹测试中，折射与零位移基线有1631个像素不同；这是材质诊断样本，不是生产内容或无线传输。30次72×53像素选中透镜绘制平均0.361ms，单次短测，不代表整体帧率、持续CPU或其它机器性能。
- WPF本机窗口交互检查8项通过：原生窗口位于当前屏幕工作区右下角且避开任务栏；352×480布局；实际页面切换后弹簧停止；连续打断导航后落到最终页；减少动效即时落位；置顶属性切换；关闭收起而非销毁；托盘恢复同一个窗口。
- 五张WPF渲染更新为小尺寸，已查看默认接收/文件/连接/设置和320×440最小窗口；底部68 DIP栏保留四个按钮，文字独立于折射材质。修复了第一轮内容透过过强、顶部内容背景没有圆角裁切的问题。
- 输入验证为应用自身的WPF状态/生命周期驱动，没有自动化物理鼠标拖动，也未覆盖混合DPI多屏移动或长期空闲资源采样。
- 本版未采集桌面或其它窗口，没有更换驱动、修改网络或运行无线引擎。iPhone原生AirDrop仍不可用。

证据：`artifacts/self-test.json`、`artifacts/material-check.json`、`artifacts/ui-check.json`、`artifacts/layout-check.json` 和PNG渲染。JSON诊断留本地，文字结论与PNG纳入提交。

## 0.1 历史基础验证

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


### 本机原始RX（17:50:43–17:51:31 +08）

用户手动绑定后，独立Radio执行--listen 5 artifacts/raw-rx.local.pcap，channel149/20MHz；初始化成功，5秒收到308帧，radio_stopped正常，进程exit0。48秒墙钟含初始化和退出，不是308帧的传输时长，也不是文件吞吐。原始PCAP和日志留本地。该测试未主动发射AWDL、未发送文件，不能替代原始TX或iPhone发现验收。

### 完整链路首次启动（17:54+08）

Radio、AWDL host、Network和TLS全部就绪；AWDL日志在2秒内出现peers1、rx action22、tx action47、tx data5。这是本机协议层的收发计数，不确认对端身份或真实文件成功。BLE Publisher一直Waiting并退出，最初监管将辅助进程退出当成致命错误，随后正常停止无线链。保留原始失败日志，已改成BLE失败显示告警且保留主接收链；延长广播启动等待至15秒。加入仅向协议层转交action/data的过滤，避免无关AP信标淹没日志；USB单播硬件重试设为7。重新构建、自检通过后重启接收，等待手机验收。
