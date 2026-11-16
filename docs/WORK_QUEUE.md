# AirDrop 当前队列

本独立目录的记录为准；父目录为原理参考工程，不是构建或运行依赖。

| 编号 | 任务 | 状态与证据 |
| --- | --- | --- |
| L01 | 小巧右下浮窗、底部四栏 | 完成：352×480默认、320×440最小，原生WPF渲染；artifacts/*.png |
| L02 | 应用内背景折射和弹簧透镜 | 完成：17项基础/材质检查；物理鼠标拖动未人工验收 |
| L03 | 置顶、托盘、工作区停靠 | 完成：8项WPF生命周期检查；混合DPI多屏未验收 |
| L04 | Windows玻璃开源参考核查 | 完成：只借鉴原理，未采用参考项目代码；THIRD_PARTY.md |
| L05a | 独立原生接收链路 | 源码/本机编译完成：devourer USB、WinAWDL/OWL、原创lwIP/命名管道桥、TLS包装与逐次确认、BLE工作进程；native/、backend/、NativeAirDropReceiver.cs |
| L05b | 软件与协议检查 | 完成：5项AWDL原生测试、84项独立上游Python测试、本地6项集成检查；VALIDATION.md。不等于无线成功 |
| L05c | c811 WinUSB、初始化和原始收发 | 本机初始化/RX通过：用户手动绑定WinUSB后probe exit0，17:50–17:51初始化与channel149/20MHz被动RX5秒，308帧，正常退出；raw-rx.local.log/pcap。后续真实iPhone传输已证明AWDL基本双向互通；驱动回滚未验收；DRIVER_RUNBOOK.md |
| L05d | iPhone真实发现/确认/文件传输 | 单次真机接收通过：18:05手机发现ASUS、逐次确认、实收3,401,455B图片、HTTP200/文件事件，大小与声明一致，2544×3408解码成功，4.226s约0.805MB/s。本地SHA256已记，未与手机源哈希对比。多文件/重复/中断/长时未验收；BLE仍未观察Started。VALIDATION.md及iphone-transfer.local.json |
| L06 | 移除导航蓝框、改名AirDrop | 完成：显式透明按钮模板，名称/标题/托盘/程序集/输出路径已改；WPF渲染、自检通过 |
| L07 | 固定版本、独立构建与许可 | 本机完成：native.lock.json、Restore-Sources/Build-Native/Package-Engine；未做干净机器完整恢复，也未对外发布 |
| L08 | 设置中的开机自启动 | 完成：默认关闭、保存后写入当前用户Run项；--startup隐藏到托盘，不自动开始无线接收。隔离注册表5项、隐藏启动/托盘恢复10项、17项基础及8项常规UI通过；实际重启/登录未验收。2026-11-14，VALIDATION.md |
| L09 | 源码/运行/研究归档分区 | 完成：源码与编译依赖保持原位；运行移至../runtime/AirDrop，89文件大小/SHA256一致；旧参考项目与UI预览归档。构建支持OutputDirectory，根入口指定唯一运行目录。迁移后17基础和6协议检查通过，运行状态恢复；详见工作区docs/DIRECTORY_LAYOUT.md |
| L10 | Wisland隔空投送控制与子窗口 | 完成：已部署至D:/AI/wisland，哈希核对+原控制页备份；同用户命名管道连接现有宿主，关闭/所有人10分钟、逐次确认、进度及完成。17基础、10Qt、4真实IPC检查通过；真实十分钟到期后界面/后台均关闭；新子窗口真实iPhone接收10,092,594B GIF成功（7.993s约1.263MB/s），文件大小/解码通过，本地哈希已记。详情integrations/wisland/README.md及VALIDATION.md |
| L11 | iPhone式内部展开与连接图标统一 | 完成：AirDrop从磁贴展开为ControlsPage内Qt.Widget；不再创建Dialog/额外原生窗口，共享玻璃背景。统一无线/蓝牙/AirDrop矢量白线图标；外点/Esc/切页收回、收起期新请求与高度/锁清理。15项Qt检查及实际应用同窗口/无原生子窗/边界报告通过；沿用原接收链，本轮无新增手机传输。见integrations/wisland/README.md |
| L12 | 内部详情展开后的背景模糊 | 完成：不透明合成后离屏模糊，边缘补齐、DPR处理；原island.content内整块背景（导航/页面/留白）连续柔化并按轮廓裁剪，消除分块接缝。尺寸变化全覆盖、合并刷新及收起清缓存。21项Qt检查及实际应用同窗口/完整覆盖报告与自身渲染核查通过，已更新D:/AI/wisland；接收后端未改动 |
| L13 | 统一隔空投送详情全部按钮 | 完成：六处按钮共用GlassButton玻璃胶囊绘制，统一圆角/高光/交互/禁用/字体，接收模式互斥且带勾号；24项Qt检查通过。已更新wisland，实际窗口buttonsUnified=true、buttonCount=6、buttonTextFits=true，连续模糊/同窗口保持通过；见VALIDATION |
| L14 | 修正Wi-Fi造型及缩小连接图标 | 完成：Wi-Fi改双同心弧/圆角扇形，三种连接底座40→32、内部24→18逻辑像素，详情标题/动画同步；控制页与明暗布局已查看，原24项Qt检查通过。已更新wisland，实际应用同窗口/按钮/背景完整覆盖保持通过；见VALIDATION |
