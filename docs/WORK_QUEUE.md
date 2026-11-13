# LiquidGlassWinAirDrop 当前队列

以本工程目录的记录为准，父目录为历史参考工程，不是本程序依赖。

| 编号 | 任务 | 状态 | 证据 |
| --- | --- | --- | --- |
| L01 | 按用户草图重做小巧竖向窗口与底部四栏 | 完成：352×480默认、320×440最小，四页面原生渲染已检查 | MainWindow.xaml、artifacts/*.png、VALIDATION.md |
| L02 | 应用内背景折射、亮边、选中透镜与可停机动效 | 完成：17项基础/材质自检通过，真实WPF弹簧落位与减少动效检查通过；物理鼠标拖动尚未人工验收 | LiquidMaterial.cs、LiquidNavigation.cs、material-check.json、ui-check.json |
| L03 | 右下工作区停靠、置顶、托盘收起 | 完成：8项窗口状态/生命周期检查通过；混合DPI多屏尚未验收 | WindowPlacement.cs、MainWindow.xaml.cs、ui-check.json |
| L04 | GitHub Windows玻璃实现调研 | 完成；仅借鉴原理，未复制或引入包 | THIRD_PARTY.md |
| L05 | 原生USB/AWDL/BLE/AirDrop整合 | 未完成，界面保持接收不可用 | ARCHITECTURE.md |
