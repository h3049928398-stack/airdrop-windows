# Wisland 隔空投送扩展

2026-11-15，本机wisland（AsamiAkito/wisland，MIT）控制页扩展。扩展原创源码保存在AirDrop独立工程integrations/wisland；上游许可证副本见LICENSE-wisland.txt。没有复制DropBridge代码或二进制。

## 操作

在wisland“控制”页点击“隔空投送”，弹出350像素宽的手机式接收面板。选择“所有人，10分钟”启动真实接收，准备通常约45秒；状态显示设备名（本机ASUS）、准备/接收状态、倒计时。选择“接收关闭”立即停止。关闭子窗口保留剩余接收时间，不改变系统Wi-Fi或蓝牙开关。

iPhone保持Wi-Fi/蓝牙开启，在分享→隔空投送中选择电脑；子窗口出现发送者和文件列表，必须点“接收”或“拒绝”。未回应45秒默认拒绝。面板显示实际上传进度及完成文件名，可打开接收文件夹。仅实现接收，没有Apple联系人身份验证，因此不提供“仅限联系人”。

10分钟由AirDrop宿主计时，即使关闭wisland也会到期停止。重新选择“所有人”会刷新10分钟。wisland关闭不强杀AirDrop；AirDrop托盘仍可停止或退出。若AirDrop早已在独立界面运行，扩展连接同一实例，避免重复USB/AWDL进程。

## 运行依赖与本机路径

- wisland：D:\AI\wisland，使用已安装的Python/PySide6环境，不修改API Key、天气/翻译/媒体/股票配置。
- AirDrop：D:\AI\AirDrop-Windows\runtime\AirDrop\AirDrop.exe，需本次新增控制接口的版本；后端、设置、密钥、接收路径保留原位。
- 缺省从wisland相邻的AirDrop-Windows工作区定位宿主。搬动后可在wisland config.json中增加airdrop.executable为完整exe路径。不要把接收后端复制到wisland或启动另一条USB链。

第一次打开面板可按需启动宿主至托盘，只有选择“所有人”才初始化无线。宿主未连接时显示离线，点击控制入口可重试。需要现有c811/WinUSB；本扩展不安装驱动或更改防火墙。

## 本机控制接口

同用户命名管道LiquidGlassWinAirDrop.Control.v1，只允许当前Windows账户访问，不监听网络端口。请求为单行UTF-8 JSON，限制4KB；每连接一次请求/响应。支持status/attach/start/stop/respond，所有状态和操作回到WPF线程处理。status返回真实运行状态、接收目录、剩余秒数、最多8个待确认请求、实际进度和完成文件名。

wisland每1.5秒轮询并续租8秒的确认界面归属；出现请求自动展示子窗口，避免同时弹出WPF确认框。wisland退出/失联后，后续请求回到原AirDrop确认界面；已委托但未确认的请求仍45秒默认拒绝。关闭收起、日志、文件安全解析和私钥继续由现有接收程序负责。

## 安装与恢复

本机安装器install.py只修改wisland/ui/pages/system.py并新增services/airdrop.py、ui/airdrop.py及本文副本docs/AIRDROP_EXTENSION.md。改动前验证控制页SHA256与已审查版本完全一致，不覆盖未知本地改动。原控制页及安装清单保存在wisland/data/extension-backups/airdrop-时间戳。

恢复前退出wisland，将备份system.py.original复制回wisland/ui/pages/system.py；根据manifest只删除本扩展新建的两个Python模块和文档，若哈希不匹配应先保留后续改动。不要删除用户config.json、models、data或原服务。已安装目录不是Git仓库，扩展代码在AirDrop源码仓库本地提交，不自行对用户安装目录初始化Git或推送。

软件/界面检查和本次真实手机验收结果见AirDrop源码docs/VALIDATION.md及docs/WORK_QUEUE.md，不能用此前独立AirDrop的一次成功代替此次新界面测试。

## 本次本机验收（2026-11-15）

- 10项Qt检查通过，含控制页磁贴布局、两种主题、启动/倒计时、逐次同意/拒绝、请求过期、关闭窗口不停止接收、真实状态显示、上传进度/完成，以及共享线程池8个任务全阻塞时专用IPC仍响应。离屏截图只作布局证据。
- 当前用户真实管道检查4项通过：状态读取、未知命令拒绝、过期请求拒绝、无效请求后服务继续响应。Release发布与原17项基础/材质检查通过。
- 已在实际wisland窗口选择“所有人，10分钟”，显示准备状态、倒计时和接收已启动。用户随后从iPhone在新子窗口中确认接收成功；实收GIF为10,092,594字节，与发送声明一致，7.993384秒，按真实文件字节数约1.263 MB/s，HTTP Upload200；文件解码640×696、32帧。记录了本地SHA256，没有手机源哈希，不声称源/目标哈希已比对。
- 修复了观察中发现的进度刷新滞后：控制通信不再与天气/媒体任务共用wisland后台线程池。重新启动wisland后，同一个宿主仍在接收窗口内，完成文件名正确同步；没有重新传输或改写用户文件。
- --airdrop启动参数可在wisland启动时直接打开子窗口，普通wisland.vbs启动仍从控制页进入。原控制页及清单备份在本机data/extension-backups/airdrop-20261115-140824。安装不包含API Key、模型或用户config.json的副本。

本机原始日志与隐私报告保留在AirDrop源码artifacts和运行版data/logs；用户收到的GIF不纳入Git。额外的大文件、多文件、失联恢复和长期稳定性仍未系统验收。

### 十分钟到期实测

实际窗口于2026-11-15 14:14:02选择开启，14:24:02.903宿主radio_stopped；十分钟后管道状态isRunning=false、remainingSeconds=null，窗口显示“接收已关闭”，三个原生工作进程与Python接收进程退出，宿主留托盘。该期间经历了wisland自身重启，截止时间仍由宿主保留并执行。真实计时关闭已验证，没有通过修改系统时钟或缩短生产计时模拟。
