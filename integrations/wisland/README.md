# Wisland 隔空投送扩展

2026-11-17：本目录已提取为[独立维护仓库](../../../wisland-connectivity-extension/README.md)。此处保留原始集成快照，新的安装/恢复、可移植测试和打包工作以独立仓库为准；当前安装不因此变动。

2026-11-16更新，本机wisland（AsamiAkito/wisland，MIT）控制页扩展。扩展原创源码保存在AirDrop独立工程integrations/wisland；上游许可证副本见LICENSE-wisland.txt。没有复制DropBridge代码或二进制。

## 操作

点击“无线局域网”或“蓝牙”磁贴也会在原灵动岛内部展开详情，开关移到详情顶部。Wi-Fi显示真实缓存网络、信号、当前连接和保存状态；选择网络后再点“连接”或“断开连接”。已保存配置直接交给Windows连接，新WPA/WPA2个人网络可输入密码，开放网络也支持连接；新WPA3/企业认证等转到系统无线设置。密码输入遮挡，取消/收起/切层时清空；只提交临时Windows配置，不写入扩展文件或日志。刷新按钮请求扫描，打开详情及定时刷新只读缓存。Windows拒绝位置权限时显示实际原因和“位置设置”入口，不自行改权限。

蓝牙详情列出已配对的经典/低功耗设备及真实连接状态，不把缓存设备都标成已连接；未知状态单独标注。设备连接/断开/新增配对从详情中的Windows蓝牙设置入口完成，不在扩展里模拟设备连接成功。开关仍调用wisland原Radio服务。三个详情层共用玻璃、连续背景模糊、Esc/外点收起与260ms展开；同时仅显示一个，AirDrop来件会切到接收确认层。

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

先在wisland自己的Python环境安装requirements-extension.txt（新增PyWinRT Windows.Devices.Enumeration 3.2.1，MIT，与原WinRT版本一致），然后运行install.py。安装器修改wisland/ui/pages/system.py，安装services/airdrop.py与connectivity.py、ui/airdrop.py、control_skin.py、backdrop_filter.py、detail_layer.py、connections.py及本文副本docs/AIRDROP_EXTENSION.md，共九项文件。首次安装核对上游原控制页；升级先核对现有安装清单，仅覆盖哈希一致的已知扩展。改动前验证控制页SHA256与已审查版本完全一致，不覆盖未知本地改动。原控制页及安装清单保存在wisland/data/extension-backups/airdrop-时间戳。

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

### 展开后的背景模糊修正

原先的缩小放大快照保留透明度，叠在仍清晰的控件上，且只覆盖展开前高度。现改为先将应用自己的控制页合成到不透明玻璃底，再用Qt离屏图像模糊；复制边缘像素后裁回，避免黑边，按屏幕缩放比例处理。效果不挂在实时控件上，详情文字和按钮保持清晰。

展开后的控制页、导航栏和留白合成为一张连续背景，同一强度过渡，详情玻璃也取该模糊上下文。详情层挂在原island.content内部，仍无额外窗口，卡片位置继续以控制页磁贴为锚点；背景按灵动岛轮廓裁剪，避免导航与页面分块产生的矩形接缝。页面伸长时先保证全区域覆盖，再采样新尺寸，避免底部短暂露出清晰控件。沿用原Backdrop更新信号，160ms合并刷新，跳过未变化帧；收起停止刷新并清空图像，不新增桌面采集线程。

透明控件使用不强制绘制默认窗口底色的离屏渲染，避免将系统调色板变成矩形色块。21项Qt检查通过，新增模糊高频衰减、边缘/DPR保真、不透明合成与清晰内容不穿透、透明页面不引入默认底色、调整大小后全区域覆盖、导航同窗口及缓存清理。诊断报告新增blurCoversPage、opaqueBlur、navBlurVisible、navSharesWindow，并在报告旁保存当前应用窗口自身的PNG，便于视觉核查。它不是手机传输或持续帧率验收。

### 所有详情按钮统一玻璃样式

详情中的“收起”“接收关闭”“所有人，10分钟”“拒绝”“接收”“打开接收文件夹”六处统一使用control_skin.GlassButton，以同一玻璃底、细边缘高光、胶囊圆角绘制，避免系统按钮和QSS使短按钮退回方形底色。尺寸按功能分为32/36/40/44逻辑像素高，圆角始终随高度变化；采用同一字体和安全文字留白，不截断短按钮文字。

接收模式为互斥选择并显示勾号，启用/主要操作为蓝色、次要操作为中性玻璃，悬停/按下/禁用/键盘焦点采用一致处理。QAbstractButton保留点击、Space键与禁用语义，禁用不会发出控制命令；点击已选“所有人”仍可刷新10分钟。没有新增网络或无线逻辑。

Qt检查总计24项通过：增加六按钮在明暗及正常/悬停/按下/禁用状态下的透明圆角、键盘互斥/禁用行为，以及含接收请求时全部六处按钮可见且文字/布局完整。布局样例artifacts/wisland-buttons-{light,dark}.png使用虚构请求，不是新的手机传输。实际应用诊断增加buttonsUnified、buttonCount和buttonTextFits。

### 连接图标比例调整

原先替换的Wi-Fi三条细曲线改为两条同心圆弧加圆角扇形底部，使用原创矢量绘制。无线、AirDrop、蓝牙图标底座统一从40缩至32逻辑像素，内部图形从24缩至18；详情标题与展开动画也采用相同32/18比例，标题控件含留边34×34。卡片点击区域及系统无线操作不改。现有24项Qt检查通过，控制页和明暗详情布局样例已重新渲染查看。

### Wi-Fi和蓝牙内部详情（后续更新）

用户要求补回Wi-Fi少掉的弧线，现为三条同心弧和圆角扇形，保留32/18紧凑尺寸。原AirDrop动画/模糊/锁与焦点清理提取为detail_layer.DetailLayer，Wi-Fi与蓝牙复用同一实现。

本机原24项检查及新增10项连接UI/服务检查通过。新增覆盖三层互斥/来件切换、明暗布局与密码表单、取消/切层/父级隐藏清密码、显式连接/断开及开关命令、蓝牙/位置设置入口、Windows ABI、临时配置XML转义/认证限制、密码不进入命令行与工作进程20秒超时、过期选择和异步连接只在查询确认后显示成功。真实只读列表读取已通过；没有自动切换当前Wi-Fi、连接蓝牙或进行新手机传输。

Windows原生调用在两个专用后台任务中运行，进一步用隐藏的有时限子进程隔离阻塞RPC；密码只经标准输入管道传递，不进入命令行。详情可见时每6秒读缓存，收起停止轮询；显式连接请求最多观察20秒，只有Windows后续列表确认状态才显示成功。诊断启动参数--wifi或--bluetooth可直接展开相应层，--connection-layout-report=完整JSON路径保存应用自身报告/PNG，不执行连接或开关操作。

API依据：[Native Wi-Fi网络列表](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlangetavailablenetworklist)、[WlanConnect](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlanconnect)、[临时连接配置](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/ns-wlanapi-wlan_connection_parameters)。PyWinRT绑定来自独立[pywinrt](https://github.com/pywinrt/pywinrt)，未引用旧参考工程。
