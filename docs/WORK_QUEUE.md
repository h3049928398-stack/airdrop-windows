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
