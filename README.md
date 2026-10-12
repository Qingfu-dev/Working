# 任务表（TaskTable）

类"游戏任务界面"的轻量桌面任务管理软件，面向游戏前端 / 美术等开发工作场景。

- 平台：Windows 10 / 11
- 技术栈：C# 12 · .NET 8 · WPF (MVVM / CommunityToolkit.Mvvm) · SQLite
- 数据与配置全部保存在本地 `%AppData%\TaskTable\`，无云端依赖

## 功能

| 模块 | 说明 |
|---|---|
| 日历总览 | 月视图四色状态：白=无任务、灰=未来计划、红=有未完成、绿=全部完成；部分完成为红绿对角混色；日历格显示当天累计专注时长 |
| 任务管理 | 增删、勾选完成、优先级、标签、来源徽标；双击日期格快速添加、全局快捷键（Alt+Space 回退链）随时发布 |
| 任务计时 | 任务绑定计时，后台持续，重启按时间戳自动续算；当日累计统计 |
| 番茄钟 | 独立模块：大号倒计时 + 环形进度（专注/暂停/休息配色），开始 / 暂停 / 跳过休息 / 重置 |
| 消费记账 | 按「吃饭 / 零食 / 购物 / 额外」四类记录金额，显示当日合计，条目可删 |
| 专注热力图 | 近一年（53 周 × 7 天）每日专注时长热力分布 |
| 模块化停靠布局 | 四个模块可拖拽重排：拖到其他模块边缘＝分屏吸附、中心＝交换位置；分隔条调宽；布局自动记忆，可一键重置 |
| 模块浮窗 | 模块拖出主窗口＝独立浮窗（可移动 / 缩放 / 置顶）；拖回模块卡片自动吸回停靠；位置与置顶状态重启后恢复 |
| 窗口 | 主窗口最大化贴合工作区（不遮挡任务栏）；模块级单独最大化 / 还原 |
| 系统集成 | 开机自启动（静默进托盘）、单实例防重复、托盘常驻、久坐 / 喝水提醒 |
| AI 发布通道 | 文件收件箱：JSON 任务文件自动解析入列（详见 `Plane.md` 的 Schema 约定） |

## 构建与运行

```bash
# 需要 .NET 8 SDK（或更高版本 SDK 编译 net8.0 目标）
dotnet build TaskTable.slnx

# 运行
dotnet run --project src/TaskTable.App
```

单文件发布：

```bash
dotnet publish src/TaskTable.App/TaskTable.App.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

## 目录结构

```
TaskTable.slnx
├── assets/
│   └── icons-source/     # 界面图标的 256px 源文件（仅收录实际使用的图标）
└── src/
    ├── TaskTable.App/     # WPF 界面层：Views（模块面板）、ViewModels、Dashboard（停靠布局容器）、Services
    ├── TaskTable.Core/    # 领域模型与核心逻辑（任务、计时、日历状态计算等）
    └── TaskTable.Infra/   # 基础设施：SQLite 仓储、收件箱监听、设置与系统集成
```

## 图标素材

`assets/icons-source/` 收录界面图标的 256px 源文件（来自图标库的 `8-界面` / `9-媒体` 分类，仅含实际使用的图标）：

| 源文件 | 库内名称 | 用途 |
|---|---|---|
| add.png | 8-界面/菜单添加 | 快速添加任务 |
| chart.png | 8-界面/网格 | 专注热力图入口 |
| close.png | 8-界面/叉号 | 关闭按钮 |
| settings.png | 8-界面/设置 | 设置入口 |
| calendar.png | 9-媒体/日历 | 标题栏图标 |
| time.png | 9-媒体/时间 | 计时徽标 |
| trash.png | 9-媒体/垃圾桶 | 删除任务 |
| play.png | 9-媒体/播放 | 开始计时 |
| stop.png | 9-媒体/停止 | 结束计时 |
| tray.png | 9-媒体/日历（白色） | 托盘图标 |

## 说明

- 支持服务的数据目录：`%AppData%\TaskTable\`（数据库、备份、日志、收件箱、布局与设置）
- 全局快捷键默认 Alt+Space，被占用时自动回退 Ctrl+Alt+Space → Alt+Q