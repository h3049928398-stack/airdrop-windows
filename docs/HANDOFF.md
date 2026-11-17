# 当前交接

2026-11-17：用户已要求将灵动岛拓展打包并建立独立GitHub私有仓库。新源码位于工作区wisland-connectivity-extension，远程为https://github.com/h3049928398-stack/wisland-connectivity-extension；后续拓展以新仓库维护，本目录integrations/wisland保留提取快照。接收端源码/当前唯一运行入口不改。41项独立检查与两份交付包核验见工作区docs/WISLAND_EXTENSION_EXPORT_2026-11-17.md。

2026-11-13：用户将软件更名AirDrop，要求移除底部导航点击后的蓝色矩形焦点框，并推进原生隔空投送。两项UI改动已完成。工程目录仍LiquidGlassWinAirDrop，是独立Git工程；不依赖父目录DropBridge代码/补丁/二进制。

已构建接收链：原创WPF监管、devourer USB工作进程、独立WinAWDL/OWL、原创lwIP IPv6/TCP/mDNS桥、独立MIT接收协议与原创逐次确认包装、原创WinRT BLE广播。Windows Job负责子进程退出清理。固定上游版本/工具链SHA256见native.lock.json；恢复/构建/打包脚本在tools。应用入口dist/AirDrop/AirDrop.exe；需要.NET10DesktopRuntime。

17项基础/材质、8项WPF、5项原生协议、84项独立上游Python、6项本地集成检查通过。后者包含合成IPv6握手与双向字节、TLS发现、拒收、允许后32768字节归档落盘/SHA256、Job清理。这不是iPhone实测。

用户已手动完成Zadig绑定。2026-11-13 17:50+08只读确认0BDA:C811为WinUSB/ProblemCode0，libusb probe exit0；内置8852BE仍Up，蓝牙BTHUSB正常。之后的初始化/原始RX/AWDL/手机验收以VALIDATION和WORK_QUEUE的最新补充为准。此前安装器启动被工具拒绝，未绕过。回滚手册和备份见DRIVER_RUNBOOK。

构建：build.ps1 -Preview；原生：tools/Build-Native.ps1；软件检查：AirDrop.exe --engine-check <报告JSON>。普通启动不自动RF，显式--receive或界面开启才启动接收。已完成一次真实iPhone发现ASUS→Windows同意→图片接收，3,401,455字节/4.226秒，图片解码通过。原始日志和本地SHA256见VALIDATION；没有手机源哈希，不声称源/目标哈希已对比。多文件和稳定性未验收，BLE仍Waiting。后续传输仍需用户在手机发起并在Windows确认。没有自动推送或对外发布。

## 2026-11-14目录迁移补充

当前运行入口已改为工作区runtime/AirDrop/AirDrop.exe；源码、.build、upstream、artifacts保留原目录，历史测试记录中的dist/AirDrop路径反映当时位置。原始运行日志、data/keys、设置及既有Received全部迁入新位置，用户自选接收目录不变。

旧参考工程已集中至../archive/DropBridge；旧预览版至../archive/old-previews。使用根Build-AirDrop.ps1构建到新runtime目录，支持-Native/-Preview；源码GUI/Native/Package脚本新增OutputDirectory用于独立指定输出。目录职责、迁移核验和恢复入口见工作区docs/DIRECTORY_LAYOUT.md与REORGANIZATION_2026-11-14.md。

## 2026-11-15：Wisland集成入口

新增integrations/wisland（MIT原创扩展及上游许可副本）。本机D:/AI/wisland控制页有隔空投送磁贴和独立子窗口；同用户命名管道控制runtime/AirDrop单实例，宿主计时10分钟，默认不自动广播，用户选择“所有人”才启动接收。详细安装、备份/恢复、协议和真机验证见该目录README及VALIDATION。

新子窗口已完成一次真实iPhone GIF接收（10,092,594B/7.993s），以及状态刷新修复；不要只看早期README中的单张图片或BLE未Started历史结论。wisland安装目录没有Git，扩展代码在本工程版本化；不要提交它的config.json、API Key、模型或用户文件。

## 2026-11-16：Wisland内部详情层（取代前版独立Dialog）

根据用户iPhone控制中心截图，AirDrop已改成控制磁贴原位展开的内部Qt.Widget，共享wisland唯一原窗口和玻璃背景。连接图标已统一为原创白线矢量，无线/AirDrop方卡与蓝牙横卡。15项界面检查及真实应用几何报告通过，无新增手机传输验收；此前11月15日协议/真机记录仍保留。

升级安装器支持现有清单哈希核对和previous逐文件备份，不修改AirDrop后端或用户配置。详情与恢复办法见integrations/wisland/README.md。

### 后续背景模糊修正

详情层现挂在同一原窗口的island.content内部，使导航、页面和留白可作为一张背景连续模糊；卡片仍以原磁贴定位，并按原灵动岛轮廓裁剪。新增backdrop_filter.py进行DPR感知的不透明合成/补边离屏模糊，不给实时控件添加effect；复用原Backdrop更新、160ms合并、收起清缓存。21项Qt检查、实际应用完整覆盖/同窗口报告和自身渲染核查通过，已更新D:/AI/wisland。证据及备份位置见VALIDATION本次补充；接收协议未改，此轮没有新手机传输记录。

### 所有详情按钮统一

收起、关闭/所有人、拒绝/接收及文件夹入口六处现统一使用control_skin.GlassButton（QAbstractButton）自绘胶囊玻璃，不依赖QPushButton/QSS；各尺寸共用圆角、字体、边缘高光和交互/禁用状态。模式互斥且有勾号，主要操作蓝色。24项Qt检查及实际窗口buttonsUnified/buttonTextFits通过，已部署；请求状态两按钮用虚构请求截图验证，此轮无新手机传输。恢复备份位置及完整证据见VALIDATION。

### Wi-Fi与蓝牙详情

两种磁贴也已改成内部展开，开关在详情中；Wi-Fi三弧恢复且保留紧凑尺寸。共享动画/模糊/生命周期移到detail_layer.py，三层互斥，来件可转AirDrop确认。新增services/connectivity.py和ui/connections.py，九项安全安装清单；PyWinRT Enumeration 3.2.1依赖见requirements-extension.txt，MIT已核对。

Wi-Fi列表、显式连接/断开与临时个人网络密码表单已实现；新WPA3/企业配置走系统设置。蓝牙显示真实已配对设备/状态，连接/断开/配对走系统设置。密码不进CLI/日志，收起/切层清空；原生调用用20秒有界隐藏进程。34项软件检查、只读1个已连接Wi-Fi/7个已配对蓝牙以及两种实际原窗口渲染通过；真实切网、蓝牙连接/电源操作未执行。实际记录、限制和备份150448见VALIDATION。
