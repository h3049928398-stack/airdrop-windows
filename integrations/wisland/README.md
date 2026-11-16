# Wisland 隔空投送扩展

2026-11-16更新，本机wisland（AsamiAkito/wisland，MIT）控制页扩展。扩展原创源码保存在AirDrop独立工程integrations/wisland；上游许可证副本见LICENSE-wisland.txt。没有复制DropBridge代码或二进制。

## 操作

在wisland“控制”页点击“隔空投送”，磁贴在控制页内部展开为300像素宽的详情层。详情层是同一灵动岛窗口内的子控件，不创建Dialog、Tool或额外原生窗口；共享原有玻璃背景，并将背景控制项柔化。选择“所有人，10分钟”启动真实接收，准备通常约45秒；状态显示设备名（本机ASUS）、准备/接收状态、倒计时。选择“接收关闭”立即停止。点击空白处、“收起”或按Esc，详情层收回至磁贴；收起保留剩余接收时间。切页/收起整个灵动岛也会清理详情层，不改变系统Wi-Fi或蓝牙开关。无线、蓝牙和隔空投送使用同一套原创矢量白线图标，AirDrop为三圈开放波纹与圆点，不使用Windows通用箭头或三角形替代。

iPhone保持Wi-Fi/蓝牙开启，在分享→隔空投送中选择电脑；详情层出现发送者和文件列表，必须点“接收”或“拒绝”。未回应45秒默认拒绝。面板显示实际上传进度及完成文件名，可打开接收文件夹。仅实现接收，没有Apple联系人身份验证，因此不提供“仅限联系人”。

10分钟由AirDrop宿主计时，即使关闭wisland也会到期停止。重新选择“所有人”会刷新10分钟。wisland关闭不强杀AirDrop；AirDrop托盘仍可停止或退出。若AirDrop早已在独立界面运行，扩展连接同一实例，避免重复USB/AWDL进程。

## 运行依赖与本机路径

- wisland：D:\AI\wisland，使用已安装的Python/PySide6环境，不修改API Key、天气/翻译/媒体/股票配置。
- AirDrop：D:\AI\AirDrop-Windows\runtime\AirDrop\AirDrop.exe，需本次新增控制接口的版本；后端、设置、密钥、接收路径保留原位。
- 缺省从wisland相邻的AirDrop-Windows工作区定位宿主。搬动后可在wisland config.json中增加airdrop.executable为完整exe路径。不要把接收后端复制到wisland或启动另一条USB链。

第一次打开面板可按需启动宿主至托盘，只有选择“所有人”才初始化无线。宿主未连接时显示离线，点击控制入口可重试。需要现有c811/WinUSB；本扩展不安装驱动或更改防火墙。

## 本机控制接口

同用户命名管道LiquidGlassWinAirDrop.Control.v1，只允许当前Windows账户访问，不监听网络端口。请求为单行UTF-8 JSON，限制4KB；每连接一次请求/响应。支持status/attach/start/stop/respond，所有状态和操作回到WPF线程处理。status返回真实运行状态、接收目录、剩余秒数、最多8个待确认请求、实际进度和完成文件名。

wisland每1.5秒轮询并续租8秒的确认界面归属；出现请求自动展示详情层，避免同时弹出WPF确认框。wisland退出/失联后，后续请求回到原AirDrop确认界面；已委托但未确认的请求仍45秒默认拒绝。关闭收起、日志、文件安全解析和私钥继续由现有接收程序负责。

## 安装与恢复

本机安装器install.py修改wisland/ui/pages/system.py，安装services/airdrop.py、ui/airdrop.py、ui/control_skin.py及本文副本docs/AIRDROP_EXTENSION.md。首次安装核对上游原控制页；升级先核对现有安装清单，仅覆盖哈希一致的已知扩展。改动前验证控制页SHA256与已审查版本完全一致，不覆盖未知本地改动。原控制页及安装清单保存在wisland/data/extension-backups/airdrop-时间戳。

恢复前退出wisland。11月16日起，每次升级将当时文件备份到previous/原相对路径，并在manifest中记录backup和created；按backup逐文件恢复到原路径。仅对created=true且当前哈希匹配本次安装的条目进行移除。旧版最初安装仍可用system.py.original恢复原控制页。若哈希不匹配应先保留后续改动。不要删除用户config.json、models、data或原服务。已安装目录不是Git仓库，扩展代码在AirDrop源码仓库本地提交，不自行对用户安装目录初始化Git或推送。

软件/界面检查和本次真实手机验收结果见AirDrop源码docs/VALIDATION.md及docs/WORK_QUEUE.md，不能用此前独立AirDrop的一次成功代替此次新界面测试。

## 本次本机验收（2026-11-15）

- 10项Qt检查通过，含控制页磁贴布局、两种主题、启动/倒计时、逐次同意/拒绝、请求过期、关闭窗口不停止接收、真实状态显示、上传进度/完成，以及共享线程池8个任务全阻塞时专用IPC仍响应。离屏截图只作布局证据。
- 当前用户真实管道检查4项通过：状态读取、未知命令拒绝、过期请求拒绝、无效请求后服务继续响应。Release发布与原17项基础/材质检查通过。
- 已在实际wisland窗口选择“所有人，10分钟”，显示准备状态、倒计时和接收已启动。用户随后从iPhone在新详情层中确认接收成功；实收GIF为10,092,594字节，与发送声明一致，7.993384秒，按真实文件字节数约1.263 MB/s，HTTP Upload200；文件解码640×696、32帧。记录了本地SHA256，没有手机源哈希，不声称源/目标哈希已比对。
- 修复了观察中发现的进度刷新滞后：控制通信不再与天气/媒体任务共用wisland后台线程池。重新启动wisland后，同一个宿主仍在接收窗口内，完成文件名正确同步；没有重新传输或改写用户文件。
- --airdrop启动参数可在wisland启动时直接打开详情层，普通wisland.vbs启动仍从控制页进入。原控制页及清单备份在本机data/extension-backups/airdrop-20261115-140824。安装不包含API Key、模型或用户config.json的副本。

本机原始日志与隐私报告保留在AirDrop源码artifacts和运行版data/logs；用户收到的GIF不纳入Git。额外的大文件、多文件、失联恢复和长期稳定性仍未系统验收。

### 十分钟到期实测

实际窗口于2026-11-15 14:14:02选择开启，14:24:02.903宿主radio_stopped；十分钟后管道状态isRunning=false、remainingSeconds=null，窗口显示“接收已关闭”，三个原生工作进程与Python接收进程退出，宿主留托盘。该期间经历了wisland自身重启，截止时间仍由宿主保留并执行。真实计时关闭已验证，没有通过修改系统时钟或缩短生产计时模拟。

## 2026-11-16：按iPhone参考改为磁贴原位展开

源码完整可用，本次直接修改Python控件，没有进行逆向或提取Apple私有资源。参考用户截图的交互和视觉层次；未把截图当作素材打包。

- 控制页为无线局域网/AirDrop方形玻璃卡片及蓝牙横向卡片；图标白线、圆形底座、启用蓝色统一，支持明暗主题。沿用原WLAN/蓝牙系统动作与AirDrop命名管道，不新增蓝牙设备枚举或伪造“仅限联系人”。
- 详情层用Qt.Widget挂在ControlsPage之下，260ms从磁贴几何展开，收回时恢复页面高度。原wisland窗口随内容高度伸缩，共享Backdrop纹理，不新增桌面采集器。背景柔化只取本应用自己的控制页快照。
- 页面切换/父级隐藏时立即清理详情层和锁；若新接收请求在收起动画期间到达，会复用同一详情卡重新展开。接收确认、上传进度、10分钟宿主计时和关闭不停止接收沿用既有逻辑。
- 15项Qt检查通过，新增同一window祖先/无顶层窗口、父级隐藏清理、重复展开、空白/Esc收起及竞态检查；三种连接图标均为代码绘制。截图中的蓝色启用状态为布局样例，不当作本机硬件状态。
- 真正运行在wisland中的应用自检报告：isWindow=false、nativeChild=false、sharesIslandWindow=true、parentIsControlPage=true、sameWindowTiles=true、cardInsidePage=true、animationStopped=true。页面616×316、详情卡300×300，均在控制页内部。
- 诊断入口：用wisland的Python启动run.pyw --airdrop --airdrop-layout-report=完整JSON路径，可复现应用自身几何检查；该检查不点击控件或启动无线。改动仅为UI和安全升级安装器，此轮未新增真实手机文件验收，11月15日成功记录仍独立保留。
