# AirDrop本机控制协议 v1

更新：2026-11-17。以`ControlPipe.cs`和`MainWindow.Control.cs`为实现依据。本文件是[灵动岛拓展](https://github.com/h3049928398-stack/wisland-connectivity-extension)接入本接收端的协议基准。

## 传输与边界

- Windows命名管道：`\\.\pipe\LiquidGlassWinAirDrop.Control.v1`。
- `PipeOptions.CurrentUserOnly`，只允许当前Windows账户；没有网络监听端口。`client`字段用于界面委托，不是身份认证。
- 每个连接一次请求/一次响应，UTF-8 JSON对象，末尾附实际换行LF。请求含末尾LF最多4096字节，服务端连接处理限时4秒。
- Python客户端当前默认超时3秒，响应上限65536字节，按LF取首个JSON对象。不要让长时间硬件初始化阻塞IPC回复。
- 正常状态包含`ok:true, version:1`；错误包含`ok:false, error:<文本>`。错误对象不保证携带其余状态字段。请求/文件名及状态显示必须保持纯文本。

## 请求

```json
{"command":"status","client":"wisland"}
```

发送时在JSON之后追加LF。可用命令：

| 命令 | 额外字段 | 行为 |
| --- | --- | --- |
| `status` | 无 | 读取实际状态；不开始RF |
| `attach` | 无 | 隐藏独立主窗口，宿主仍在托盘；不开始RF |
| `start` | 无 | 将宿主截止时间设为当前UTC加10分钟；未运行时异步准备接收，重复选择会刷新期限 |
| `stop` | 无 | 清除截止时间并异步停止，随后清理待确认请求 |
| `respond` | `id`字符串、`accept`布尔 | 只回答该待确认请求；过期/未知id返回错误，不自动同意其他请求 |

带`client:"wisland"`的请求使界面委托租约延长8秒；租约在命令分发前更新。拓展每1.5秒读状态并续租。租约有效时新来件交给拓展，失联后后续来件回到WPF确认界面；已委托而未回答的请求仍由接收工作进程45秒超时拒绝。

## 状态样例与语义

```json
{
  "ok": true,
  "version": 1,
  "isRunning": false,
  "isStarting": false,
  "message": "接收已关闭",
  "deviceName": "Windows PC",
  "receiveDirectory": "D:\\Received",
  "remainingSeconds": null,
  "requests": [],
  "transfer": {"phase":"idle","bytes":0,"total":0,"lastFile":""}
}
```

| 字段 | 用法 |
| --- | --- |
| `isRunning` / `isStarting` | 当前接收链状态；不能把start响应当作已经准备完毕 |
| `message` | 当前真实状态/错误，直接按纯文本显示 |
| `deviceName` / `receiveDirectory` | 当前配置；目录是用户信息，不写入公开诊断/截图 |
| `remainingSeconds` | 宿主剩余秒数或null；客户端不另建独立的十分钟期限 |
| `requests` | 最多8个：每项`id,sender,files`，每项最多6个文件名；以id逐次同意/拒绝 |
| `transfer.phase` | `idle`、`receiving`、`received`、`failed`；是显示状态，不代替文件校验 |
| `transfer.bytes` / `total` | 实际进度；total可能为0，UI应支持不确定进度 |
| `transfer.lastFile` | 最近完成的文件名文本，最多6个名称连接后返回 |

start、stop、respond的响应是受理时的快照，实际操作异步执行；客户端必须继续轮询。用户收起详情或退出wisland不发送stop，也不强杀宿主。宿主保持截止时间，十分钟到期停止链路；宿主退出则Windows Job清理其工作进程。

十分钟期限由此IPC的start路径建立，不能据此推断独立WPF界面手动开启或--receive路径也会自动设置相同期限；需要统一行为时应明确修改并验收各入口。

## 兼容变更与联调

新增可选状态字段应保持旧客户端容错。变更管道名、编码、大小、确认语义或命令字段前应提供新版本或兼容层，并同时更新两仓库文档/适配器。文件名长度、请求数和JSON转义可能影响客户端64KiB限制，不能只增加返回字段而忽略边界。

先用模拟transport验证错误、超时、过期id、请求切换、进度及宿主计时，再做当前账户只读status检查。联调start/stop/respond会影响现有接收会话，必须沿用用户会话授权，不在普通测试中自动调用。最后由用户手机完成真实发现、确认和文件验收。
