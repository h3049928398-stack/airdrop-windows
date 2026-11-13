# 独立接收架构

2026-11-13：按用户最新要求，软件显示名和可执行文件改为 AirDrop；工程目录/命名空间仍保留旧名以避免无关搬迁。底部Liquid Glass、小尺寸和右下停靠保持。

本工程不复用DropBridge源文件、补丁或二进制。独立采用用户允许的开源基础组件，完整提交锁定在native.lock.json。原创应用与各协议/USB工作进程通过管道和本地流通信，保留各自许可；这不代表整个发行物可按MIT分发。

```text
WPF AirDrop.exe + Windows Job + 每次请求确认
  ├─ AirDrop.Radio.exe：devourer C8821C/libusb，限定0bda:c811
  │   └─ WinAWDL v3 radio管道：HELLO/RX/TX/channel/address
  ├─ AirDrop.Awdl.exe：独立WinAWDL/OWL，原创host_if命名管道
  ├─ AirDrop.Network.exe：lwIP IPv6/TCP/UDP + 原创mDNS/透明TCP桥
  │   └─ 原始IPv6包 ↔ AWDL host管道；TLS字节 ↔ 127.0.0.1:18771
  ├─ Python接收进程：独立MIT协议模块 + 原创逐次确认包装
  └─ PowerShell BLE进程：Windows WinRT Publisher辅助发现
```

默认channel149/20MHz、一条空间流。没有系统TUN适配器、网络桥配置或防火墙修改。原生管道使用OVERLAPPED以避免全双工读写互锁；队列有上限。网络栈所有lwIP操作在单一线程，管道线程只交换字节。IPv6端口8771代理TLS字节到本地18771，证书自签，不伪造Apple证书或身份记录。

TLS接收器仅监听loopback。每次Ask经JSON事件发送给WPF，45秒未同意默认拒绝；Upload没有对应批准会话会拒绝。接收归档的路径限制/配额/临时发布由独立MIT transfer/cpio模块处理；原有ReceiveStore仍用于应用基础存储自检，不把它冒称当前归档实现。

BLE辅助进程失败仅显示告警，不停止主接收链；Windows实际广播是否成功以日志为准。启动顺序：只读硬件/WinUSB检查 → 本地TLS → USB初始化 → AWDL和IPv6桥 → BLE。日志有时间、会话状态；单文件超过5MB轮转一次。停止先信号并清理子进程，USB最多留10秒结束；Windows Job兜底父进程退出。尚未验证硬件中断恢复和持续负载，不承诺自动恢复所有USB固件异常。

GUI通过SetupAPI检查设备，目录监控只列真实文件。底部GlassNavButton透明模板消除蓝色矩形焦点框，选中玻璃透镜、文字和键盘切换保留。材质只采样应用自己的ContentLayer，按需刷新，不采集桌面。

剩余验收按WORK_QUEUE中L05c→L05d执行：当前原始RX已通过，TX与手机传输未验收，不以源码存在、测试通过或上游报告替代本机实物和iPhone结果。
