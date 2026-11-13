# 本机绑定与恢复

仅适用于本机专用USB网卡0BDA:C811。用户已授权此目标的WinUSB切换、初始化、AWDL/BLE和iPhone测试，不需要再次索要任务许可。不要触碰内置RTL8852BE或独立蓝牙0489:E123，也不要按别的c820设备经验选择MI_02。

绑定前只读证据：RtlWlanu、netrtwlanu.inf、1030.38.712.2019、ProblemCode0；内置8852BE承担当前Wi-Fi连接。USB改WinUSB后将退出普通Wi-Fi用途；若当前电脑改用该USB上网，则会中断该接口网络。独立项目不会自动修改驱动。

## 工具与备份

- 官方工具：`.build/setup/zadig-2.9.exe`，来自 https://github.com/pbatard/libwdi/releases/tag/v1.5.1 ，本机Authenticode Valid、Akeo Consulting；SHA256 `4ECAA95DF3DA3621486A043AEF8B3050B8BAFE7C901402871E816229EF82039B`。没有启动它，也没有使用DropBridge包内工具。
- 原驱动副本：`.build/driver-backup/netrtwlanu.inf_amd64_2814b0012fc3a833/`。INF SHA256 `40ABAA88EDBD247342DE443E89CCF9B39AB89944A2288A0138630A90AC582206`，SYS SHA256 `6EF2E531AE7A8A636FDED68EA679BCCF692758D905BEC35053A240131ADA3463`。原系统驱动包未删除。
- 先前电脑控制工具拒绝启动Zadig，原始信息“Computer Use was not approved to use 9”，无具体原因。本工程不通过另一执行工具规避，已请用户手动打开并绑定。授权存在不等于安装已完成。

## 手动绑定

打开官方Zadig，Options → List All Devices，核对USB ID必须为0BDA C811，当前驱动RtlWlanu；右侧选择WinUSB，点击Replace Driver。若ID不同、出现多个无法区分的目标或安全设置错误，停止并保留日志；不禁用系统安全功能。

完成后先运行应用只读连接检查和 `backend/AirDrop.Radio.exe --probe`。只有WinUSB正常打开后，才进行已授权的有限原始RX与后续AWDL接收。普通WPF启动本身不会自动发射无线帧。

## 恢复

先从AirDrop托盘退出。设备管理器选择同一0BDA:C811 → 属性 → 驱动程序 → 回退驱动程序（可用时）；否则更新驱动 → 浏览我的电脑 → 从可用驱动列表选择原Realtek Wireless LAN驱动。需要手动指定时使用上述备份目录中的netrtwlanu.inf，原包保留在Windows DriverStore。没有在本轮演练实际回滚，不把备份存在称为回滚成功。

恢复后重新插拔此USB网卡，检查RtlWlanu/ProblemCode0及普通Wi-Fi功能；保存时间和结果。本手册不提供广泛设备删除、强制驱动卸载或防火墙命令。

2026-11-13 17:50+08用户手动绑定完成，已确认WinUSB/ProblemCode0、probe exit0，5秒被动RX收到308帧。尚未进行实际恢复演练。
