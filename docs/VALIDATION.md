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

### 修复后链路观察（17:57+08）

第二次USB初始化约43秒。AWDL、IPv6桥和TLS已持续运行超过30秒，peers1且action收发持续增长；BLE等待15秒仍Waiting并退出后，主链保持运行，故障隔离生效。本机另行向127.0.0.1发Discover确认200/ReceiverComputerName=ASUS，该查询是本地诊断，不能当成手机请求。等待用户手机分享列表反馈，真实文件传输尚无证据。

### BLE等待语义修正（18:00+08）

Microsoft官方枚举说明Waiting为等待服务时间，Started才是已广播： https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.advertisement.bluetoothleadvertisementpublisherstatus 。原15秒后退出逻辑会丢弃排队请求，已改成保持请求，状态变化才记录，Aborted才退出；增加Seconds参数供有时限硬件验证，finally停止Publisher。

ValidateOnly构造通过；独立30秒有时限广播请求全程仅Waiting，结束exit0且主动Stop，未观察Started，不能声称BLE广播成功。该测试不修改蓝牙驱动、服务或其它应用。应用主链继续运行，等待手机反馈。脚本已复制到本机运行目录，下次启动辅助发现时生效。

## 2026-11-13 18:05 +08：首次真实 iPhone 原生接收成功

用户确认在iPhone隔空投送分享列表看到了ASUS，并尝试发送。随后检查到真实协议记录：18:05:23 Discover200，18:05:25 iPhone Ask单个图片，18:05:27.623确认后Ask200；18:05:27.7713859 transfer_started，18:05:31.9969228 files_received，Upload200。

- 实际落盘：3,401,455字节，与Ask声明的3,401,455B一致。文件名和绝对路径保留在本地报告及原始日志，不把用户图片纳入Git。
- 本机System.Drawing解码成功，2544×3408像素；没有重编码、转换或覆盖接收文件。
- 本地SHA256：CF696D42E754AA844BFA293E7E2310F4E4EFF16B886D413DC2299F23B56F2475。没有手机源文件的哈希，未完成源/目标哈希对比，不能将计算本地哈希说成端到端哈希一致。
- 起始至落盘4.2255369秒；按实际文件字节数计算0.8049758 MB/s（十进制MB）。包括约2秒归档尾标记宽限/收尾，不用PHY速率或网络归档压缩字节代替文件大小。该次完整归档未带结束标记，协议在宽限后保留完整文件并回复200；日志已保留。
- 证据：artifacts/iphone-transfer.local.json、dist/AirDrop/data/logs/engine.log以及data/Received中的原文件。此前17:57:48人工本地Discover单独标注，不混作手机事件；本次18:05手机发起与文件事件匹配。
- 成功范围：本机现有RTL8811CU/WinUSB + 独立接收链，单个真实iPhone图片，发现、用户确认和落盘通过。BLE仍没有Started证据；此次发现成功不能反推BLE已广播。没有额外iPhone应用、WSL/虚拟机或DropBridge运行依赖。
- 尚未覆盖：重复/多文件/大视频、取消/断线/重启/驱动恢复、长期稳定性、其它iOS和其它机器。应用保持运行，未打断用户继续使用。

## 2026-11-14：开机自启动

- 设置新增“开机自启动”，默认关闭，点击保存设置后生效。开启/关闭仅写入/删除HKCU Run下本应用独有的LiquidGlassWinAirDrop值，不需要管理员权限；命令引用完整exe路径并带--startup，支持空格和Unicode路径。
- 启动参数--startup只启动应用并隐藏到托盘，不自动开启USB/AWDL接收。双击托盘恢复原窗口；启动时先透明再隐藏，避免窗口闪现。保存设置失败会恢复原启动项，启动时从系统注册读取当前注册状态。预览/UI检查不修改真实启动项。
- Release发布成功，17项原基础/材质和8项常规WPF检查通过。新增隔离HKCU测试5项通过：默认关闭、带空格/Unicode路径注册、关闭只删自己的项、保存失败还原已开启项、开启失败不遗留项。测试子键清理完毕，未启用真实开机启动。
- --startup-check的10项WPF检查通过，含隐藏启动时托盘存在，以及恢复后窗口可见/不透明；Settings原生渲染已查看，新增选项在小窗内可见，其余设置和保存按钮仍可滚动访问。
- 本机应用已更新并重新启动，用户设置、接收文件与后端目录保留。未更换驱动、重启系统或执行真实注销/登录，因此不把参数启动测试称为真实登录验收。
- 证据：artifacts/startup-registration.json、startup-ui-check.json、self-test.json、ui-check.json、settings.png。真实Run项是否启用由用户在设置中决定。

## 2026-11-14：整理迁移与显式输出验证

当前运行目录为工作区runtime/AirDrop。迁移前后89个文件逐项大小/SHA256相同，保留数据和日志；从新目录运行基础17项及本地协议6项检查通过。构建新增OutputDirectory，在.build/layout-check独立发布通过17项检查，打包六个原生/协议入口与迁移运行版哈希相同；原生C++源码和二进制没有因布局而重编译。

整理中发现无自有工作进程的临时NativeAirDropReceiver释放时也会发送全局停止事件（受限环境表现为UnauthorizedAccessException），这可能误停当前接收链。已在StopCore/Dispose中按自有进程列表加保护；保存失败报告，修复后自检通过。该修复已更新到新运行版；设置、密钥和既有文件不覆盖。详细迁移记录见工作区docs/REORGANIZATION_2026-11-14.md。

## 2026-11-15：Wisland控制中心与手机式子窗口

- 为D:/AI/wisland新增“控制→隔空投送”磁贴及独立子窗口；风格跟随主题，350像素宽，接收关闭/所有人10分钟、设备名、状态/倒计时、接收/拒绝、实际进度/完成和打开目录。控制页原WLAN、蓝牙、代理/音量等功能保留。原控制页先SHA256核对并备份，配置、模型、其它服务不复制或重写。
- AirDrop新增当前用户专用命名管道控制接口，JSON请求限4KB、超时4秒、不开放网络端口。wisland只连接单实例宿主，按需启动至托盘；start/stop/respond通过WPF线程操作既有NativeAirDropReceiver。无需新网卡或iPhone应用，不复制DropBridge源码或二进制。确认租约8秒、每1.5秒刷新，失联后新请求回退原WPF确认，未确认请求45秒拒绝。
- 17项基础/材质、10项Qt扩展检查、4项真实IPC检查通过。新子窗口实际点击“所有人”后准备/运行/倒计时通过；Qt测试还覆盖关闭不停止、过期请求、指令映射、明暗布局及全局8线程被阻塞时独立控制线程仍响应。
- 真机：用户报告在新子窗口确认接收成功。具体本机时间（Asia/Hong_Kong）为14:16:17 iPhone Ask，14:16:33.4429844 transfer_started，14:16:41.4363684 files_received，HTTP Upload200。文件10,092,594字节，与声明一致；GIF可解码640×696、32帧。实际字节/7.993384秒=1.262618MB/s（十进制）。本地SHA256 ECAAB946F31A8FA254D267189A34F4CDAF566ABD7794C00DBA8526DA88E44887；没有手机源哈希，未进行源/目标哈希比对。
- 接收已完成但Qt进度最初滞留旧值，宿主状态正确；控制端改为专用单线程IPC执行器及Qt队列信号，避免共用后台池的阻塞，实际窗口已核对“接收完成”及文件名。修复前后原始日志保留。
- 本机数据：artifacts/wisland-qt-check.log、wisland-iphone-transfer.local.json、wisland-airdrop-{light,dark}.png、wisland-controls.png、runtime/AirDrop/data/logs/engine.log。用户文件不提交，离屏截图不充当真实手机传输证据。
- Wisland本机为下载解压目录，无Git。扩展原创源码、安装器、上游MIT许可证副本及文档保存在integrations/wisland，通过原文件哈希和安装清单防止覆盖未知修改。安装备份路径见该目录README。此次未进行多文件/大视频/长期或其它机器系统验收。

### Wisland十分钟自动停止实测

2026-11-15 14:14:02从真实子窗口选择所有人10分钟，14:24:02.9032178日志radio_stopped；管道isRunning=false/remainingSeconds=null/phase=idle，实际子窗口文本“接收已关闭”。仅剩AirDrop宿主进程，无Radio/AWDL/Network/Python接收进程。截止时间跨wisland重启仍保持，由宿主执行；实际等待完整10分钟，未调整系统时钟或生产计时参数。
