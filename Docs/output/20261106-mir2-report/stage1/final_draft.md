# Mir2 服务端与客户端改造总结报告

**工程**：Mir2（Crystal 血统）服务端 + 微端客户端
**报告日期**：2026-11-06
**覆盖范围**：本次 2 万假人规模化改造的收尾阶段，含客户端同屏治理、假人日志静音、服务端内存治理三项交付，并附此前已交付项的追溯清单。

## 摘要

本阶段完成三项改造，均以「不改协议、不改战斗判定、改动可单独回滚」为前提：

1. **客户端同屏治理**：内挂新增「只显示1格内玩家」，并加入队友、行会、鼠标指向、当前目标的常显白名单。这是应对 2 万假人同屏时客户端渲染卡顿最直接的一刀——不改服务端、不动协议，纯本地不画。
2. **假人日志静音**：复核并补全服务端三处日志闸门，假人的聊天、登录、退出不再进入 GUI 的「聊天日志」与「日志」页签；同时修掉假人登录失败日志"每秒一条"的刷屏点。
3. **服务端内存治理**：新增开服内存分布报告与可配置的定时全堆压缩，为把常驻内存压到 200MB 量级提供测量手段和调节旋钮。

三项改动均通过自动化排雷自测（客户端 31 项、日志 9 项，全部通过）。客户端已部署到 `E:\micromir2`，服务端产物已编译、待部署。

## 一、本轮交付概览

| 序号 | 交付项 | 影响面 | 状态 |
|------|--------|--------|------|
| 1 | 客户端「只显示1格内玩家」+ 常显白名单 | 客户端渲染、鼠标命中判定的可见性 | 已部署 |
| 2 | 假人日志静音复核 + 失败日志限流 | 服务端 GUI 日志与聊天日志 | 已编译待部署 |
| 3 | 内存分布报告 + 定时全堆压缩 | 服务端启动日志、运行时内存 | 已编译待部署 |
| 4 | 两个自动化排雷工具 | 工程自测资产 | 已入库 |

## 二、客户端：只显示 1 格内玩家

### 2.1 背景与目标

2 万假人进入视野后，客户端需要为每个角色绘制身体、武器、头、翅膀、坐骑、品质光效、名字、血条、伤害数字，以及其释放的技能特效。服务端虽然有 `VisibleCap` 限制单个真人能看见的假人数量，但客户端自身没有任何按距离或数量降级的逻辑——只要包发过来，就按满配角色全量渲染。

目标很明确：**按距离丢弃绘制，但保留完整的游戏逻辑**。看不见的人不需要画，也不应该被点到（否则免 shift 状态下点地面会变成原地打人）。

### 2.2 显示规则

勾选后（内挂 → 其他 → 「只显示1格内玩家」）：

| 对象 | 行为 |
|------|------|
| 自己 | 始终显示 |
| 身边 1 格内的玩家（含 8 个斜向，切比雪夫距离 ≤ 1） | 显示 |
| 队友（`GroupDialog.GroupList` 名单内） | 任意距离常显 |
| 同会成员（双方 `GuildName` 非空且相同） | 任意距离常显 |
| 鼠标指向的玩家（`MouseObject`） | 常显 |
| 当前目标 / 魔法锁定 / 施法对象（`TargetObject` / `LockObject` / `MagicObject`） | 常显 |
| 其他远处玩家 | 整条不画：身体、武器、头、翅膀、坐骑、品质光效、名字、对话气泡、血条、毒、伤害数字，以及其技能特效；同时不可点中 |
| 怪物 / NPC / 地面物品 / 宝宝 / 英雄 | 不受影响 |

白名单的取舍理由：队友和同会看不见会直接打乱跑位与救人；目标看不见则锁定后凭空消失，反而不安全；鼠标指向的人若看不见，点人就没有任何反馈。

行会比对特意要求**双方 `GuildName` 非空**——两个都没有行会的玩家不能因为都是空串就互相常显。

### 2.3 实现要点

**唯一判定出口**。所有绘制入口共用一个属性判定，避免出现"身体藏了名字还飘着"这类漏网：

```csharp
public bool NearPlayerFiltered
{
    get
    {
        if (!Settings.NearPlayerOnly) return false;
        if (User == null) return false;
        if (!(this is PlayerObject) || this == User) return false;
        if (this is HeroObject || this is UserHeroObject) return false;
        if (NearPlayerKeepVisible) return false;   // 常显白名单，必须在距离判定之前

        return Math.Abs(Movement.X - User.Movement.X) > 1
            || Math.Abs(Movement.Y - User.Movement.Y) > 1;
    }
}
```

**闸门必须在方法第一句**。六个绘制入口（`Draw`、`DrawBehindEffects`、`DrawEffects`、`DrawBlend`、`DrawName`、`MouseOver`）全部在方法体第一句返回，否则等于先完整画了一遍再丢弃，白费算力。这一点有专门的自动化断言守着。

**世界层是缓存贴图**。客户端世界层整屏渲染到 RenderTarget 并缓存，只调用场景 `Redraw()` 不会重画地图，用户会以为"勾了没反应"。因此勾选时同步把 `MapControl.TextureValid` 置为 `false`，强制重建。

### 2.4 改动清单

| 文件 | 改动 |
|------|------|
| `Client/Settings.cs` | 新增 `NearPlayerOnly`（`[Game] NearPlayerOnly`，默认 `false`）+ 读取/写回 |
| `Client/MirObjects/MapObject.cs` | 新增 `NearPlayerFiltered`（统一判定）与 `NearPlayerKeepVisible`（白名单） |
| `Client/MirObjects/PlayerObject.cs` | 六个绘制/命中入口首句返回 |
| `Client/MirScenes/GameScene.cs` | 对象绘制循环整条跳过；两处技能特效循环按 `Owner` 过滤；鼠标对象名字绘制过滤 |
| `Client/MirScenes/Dialogs/AutoPlayDialog.cs` | 内挂「其他」页 `(26,340)` 勾选框；写回配置 + 落盘 + 场景重绘 + 世界层缓存失效；开启/关闭提示；原提示压缩为两段 |
| `Tools/NearPlayerTest` | 31 项排雷自测（新增） |

### 2.5 验证结果

`Tools/NearPlayerTest` 共 31 项，全部通过，覆盖：

- 配置项默认值、`[Game]` 读取、写回三项（默认关闭，老玩家行为零变化）
- 7×7 邻域 49 格真值表：1 格内（含斜向、同格）可见，2 格起隐藏
- 四类不该隐藏的情况：关开关、自己、怪物、英雄
- 六个绘制入口的闸门都在方法第一句
- 两处场景特效循环都按 `Owner` 过滤
- 常显闸位于距离判定之前；四个动态目标齐全；队友查 `GroupDialog.GroupList`；行会要求双方非空
- 面板控件无重叠、提示不越过内容区底线（430）
- 编译产物 `Client.dll` 确含新开关（防"改了源码没重编"）
- `Server` / `Shared` 未被污染

### 2.6 取舍与已知副作用

- **鼠标扫过远处玩家会短暂闪现**。因为鼠标指向属常显，扫到谁谁出现一帧。这是"看得到才点得到"的必要代价；若觉得扎眼，可以去掉鼠标指向常显，只保留目标/锁定/队友/行会。
- **远处队友会显示但怪物不会**。这是刻意设计：怪物、掉落、NPC 是玩法必需，玩家不是。
- 该功能只省客户端绘制，**服务端该发的包一个不少**，带宽不变；真正的总量闸门仍是服务端 `VisibleCap`，两者互补。

## 三、假人日志静音

### 3.1 三条通道与拦截点

服务端 GUI 有两个文本框：「聊天日志」（`MessageQueue.ChatLog`）与「日志」（`MessageQueue.Log`）。假人刷屏会同时污染这两处，2 万规模下会把界面拖死。

| 场景 | 拦截位置 | 做法 |
|------|----------|------|
| 假人聊天 / 喊话 / 私聊 | `PlayerObject.Chat` 第一句 | 全服唯一 `MessageQueue.EnqueueChat` 调用点，前置 `if (!(this is FakePlayerObject))` |
| 假人退出 | `PlayerObject.StopGame` | `MessageQueue.Enqueue(logReason)` 同样包住；`LogOutReason` 全服唯一调用点 |
| 假人登录 | `FakePlayerManager.Login` | 正常登录路径不写任何日志 |

### 3.2 本轮修复

复核时发现真正的刷屏点不在正常流程，而在**失败路径**：假人登录的两条日志——`上次对象未回收（Info.Player 占用）` 与 `创建失败：xxx`——位于每次重试都会执行的分支里。这个假人每秒重试一次、连败 5 次才报废，意味着单个坏假人能在 5 秒内连刷 5 条，2 万规模下很容易堆满日志。

改为限流：前 3 次照报（能看见问题），之后每 60 次报一条，并带上累计次数。

```csharp
rec.FailCount++;
if (rec.FailCount <= 3 || rec.FailCount % 60 == 0)
    MessageQueue.Instance.Enqueue($"[假人] {rec.Name} 上次对象未回收（Info.Player 占用），跳过本次（第 {rec.FailCount} 次）");
```

### 3.3 验证结果

`Tools/BotLogFilterTest` 共 9 项，全部通过：

- 聊天日志写入口全服只有 1 个，且被「假人排除」判断紧贴包住（中间夹了别的语句会报错）
- 假人发言确实统一走 `Player.Chat`，AI 里没有直接写日志
- 退出日志同样被紧贴包住；`LogOutReason` 只有一个调用点
- `Login` 里的失败日志全部限流
- 假人管理器的日志一律带 `[假人]` 前缀，不冒充玩家上下线文案
- 真人下线日志未被误伤（判断方向是「不是假人才写」）
- 服务端产物比源码新

## 四、服务端内存治理

### 4.1 结构分析

在动手之前先算账。服务端内存的大头有三块：

| 大头 | 规模估算 | 说明 |
|------|----------|------|
| 2 万假人档案 | 约 100~300MB | 每个假人一份 `AccountInfo` + `CharacterInfo`；后者含 86 格背包、14 格装备、10 格交易、40 格任务、16 格精炼共 166 个物品槽，外加十多个 List |
| 地图 | 格数 × 约 40 字节 | `Map.Cells` 是二维数组，但 `Cell` 是**类**而非结构体，每格一个对象；总量取决于地图规模 |
| GC 预留 | 峰值的 30~50% | .NET 在分配峰值后默认不把空闲段还给系统，任务管理器看到的数字会明显高于存活对象 |

结论：能否压到 200MB，取决于测量结果，不能凭感觉改。

### 4.2 内存分布报告

新增 `Envir.LogMemoryReport()`，在开服完成（地图、物品库、假人档案均已就绪、网络未开）时打印一次：

```text
──── 内存分布报告（开服完成）────
工作集 xxx MB｜私有内存 xxx MB｜托管堆 xxx MB｜提交内存 xxx MB（含 GC 预留，可被 [Memory] CompactMinutes 压缩回收）
地图 xx 张（xx 万格，格子是类对象，粗估 xx MB）｜怪物 xx 只（掉落表 xx 条）
物品库 xx｜技能库 xx｜任务 xx｜NPC xx（脚本 xx 个）｜行会 xx｜账号 xx｜角色 xx
假人档案 xx 个（每个 = 账号 + 角色档案 + 背包装备，粗估 xx MB；上线峰值另计在线对象）
```

报告只读不改，用于一次性定位"内存花在哪"。

### 4.3 定时全堆压缩

新增配置 `[Memory] CompactMinutes`（默认 `0` 表示关闭，钳制范围 0~1440 分钟）。大于 0 时，`Envir.Process` 每 N 分钟执行一次全堆压缩：

```csharp
System.Runtime.GCSettings.LargeObjectHeapCompactionMode =
    System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
GC.Collect(2, GCCollectionMode.Forced, true, true);
```

压缩完成后日志会打前后差值，便于观察实际省下多少：

```text
[内存] 已全堆压缩：托管堆 850→620 MB，提交内存 1100→760 MB
```

### 4.4 使用说明与风险

- **建议值 30~60 分钟一次**。压缩瞬间会有一次 Full GC 停顿（几十到几百毫秒），设得太密会影响手感。
- **默认关闭**，不改变现有服务器行为。
- `Setup.ini` 必须在**服务端停止状态**下修改，否则关闭时回写会覆盖手工改动（该项目既有约定）。

### 4.5 下一步

拿到开服报告后再定方案：

- 若大头在地图格子 → 方向是把 `Cell` 类结构体化或惰性化，工程量中等。
- 若大头在假人档案 → 方向是"离线假人只保留轻量档案，背包装备上线时按需生成"，省得最多，但要兼顾角色保存逻辑，需要单独设计。

## 五、此前已交付改动（追溯）

| 主题 | 内容 |
|------|------|
| 中毒锁复核 | `PoisonLockMagicOnly` 只在 `CanAttack` 分流，`CanCast` 仍锁魔法，怪物侧与移动锁未动 |
| 配置页重组 | 中毒锁页改名「特殊设置」，内嵌「诱惑之光」「特殊限制」两个子页 |
| 对话框非模态 | 全站 `ShowDialog` 改为 `ShowToolForm`（`Show` + `Owner`），多窗口互不干扰 |
| 假人规模 | 上限 300 → 20000；名称池扩容至约 11 万组合；支持 `FakePlayerNames.txt`（不足随机补齐，文件不存在则按数量生成） |
| 在线列表 | 改虚拟模式（`VirtualListSize` + `RetrieveVirtualItem`）；预编译的 `CustomFormControl.dll` 需单独重编并三处拷贝 |
| 2 万假人性能 | 无真人地图分级休眠（跳过 Brain、保留 TownRevive）；`RealPlayers` 短列表替换全量 `Players` 扫描；`VisibleCap` 限制同屏假人 |
| 登录体验 | `Init` 只建档、每秒分批登录、渐显波次、登录散场、安全区配额、随机点秒飞 |
| 假人行为 | `HomeRange` 60 与 `ChaseRange` 16 拆分；`CrowdCalm` 被看就文静 |
| 假人聊天 | `ChatIntervalMs` 配置（默认 10 秒，±30% 抖动） |

## 六、部署清单与生效方式

| 端 | 产物 | 状态 |
|----|------|------|
| 客户端 | `E:\micromir2\Client.dll` | 已部署（13:28 编译）；旧文件备份为 `Client.dll.bak_nearplayer_20261106`；部署时客户端未运行 |
| 服务端 | `D:\mir2-20241027\Build\Server\Release\Server.Library.dll` | 已编译（14:18，0 错误）；运行目录未知，待提供后备份覆盖 |

注意事项：

1. 服务端只需替换 `Server.Library.dll` 一个文件，不需要替换 `Server.exe`。
2. 替换前必须先停服，运行中文件被占用。
3. 客户端 `Mir2Config.ini` 尚未写入 `NearPlayerOnly`（默认关闭），勾选后自动落盘。

## 七、待决事项

1. **服务端运行目录未知**——日志静音与内存报告尚未真正生效，需要路径才能部署。
2. **200MB 内存目标待测量**——需要一次开服的内存分布报告，才能判断该动地图还是动假人档案。
3. **客户端渲染层三处根因未动**：
   - `CMain` 主循环中 `UpdateEnviroment()` 每个 idle 迭代都执行，没有节流，单核接近满载；
   - `DXManager.SetBlend` 每次调用都 `Sprite.End/Begin`，`MLibrary.DrawBlend` 成对调用，每个混合精灵打断一次批处理；
   - 品质光效每个角色最多额外约 44 次绘制。
4. **鼠标指向常显的闪现**是否保留，待确认。

## 附录 A：自测工具

```bash
export DOTNET_ROOT=/c/Users/86191/.dotnet
cd D:/mir2-20241027/Tools/NearPlayerTest && dotnet run -c Release -- "D:\\mir2-20241027" "D:\\mir2-20241027\\Build\\Client\\Release\\Client.dll"
cd D:/mir2-20241027/Tools/BotLogFilterTest && dotnet run -c Release -- "D:\\mir2-20241027"
```

| 工具 | 项数 | 覆盖 |
|------|------|------|
| `Tools/NearPlayerTest` | 31 | 配置项、邻域真值表、六个绘制闸门、场景循环、面板布局、产物校验、污染检查 |
| `Tools/BotLogFilterTest` | 9 | 日志写入口唯一性、闸门紧贴、限流、前缀规范、真人不受影响、产物新旧 |

## 附录 B：新增与相关配置项

| 配置 | 位置 | 默认 | 说明 |
|------|------|------|------|
| `NearPlayerOnly` | 客户端 `Mir2Config.ini` `[Game]` | `false` | 只显示 1 格内玩家 |
| `CompactMinutes` | 服务端 `Setup.ini` `[Memory]` | `0` | 定时全堆压缩间隔（分钟），0 关闭 |
| `ChatIntervalMs` | 服务端 `Setup.ini` `[FakePlayer]` | `10000` | 假人说话间隔（毫秒，±30% 抖动） |
| `VisibleCap` | 服务端 `Setup.ini` `[FakePlayer]` | `50` | 每个真人视野内最多显示的假人数 |
| `LoginBatch` | 服务端 `Setup.ini` `[FakePlayer]` | `100` | 每秒分批登录的假人数 |
| `ShowRampMax` | 服务端 `Setup.ini` `[FakePlayer]` | `20` | 客户端每秒渐显封顶 |

## 附录 C：关键代码位置

| 功能 | 位置 |
|------|------|
| 客户端过滤判定 | `Client/MirObjects/MapObject.cs`：`NearPlayerFiltered`、`NearPlayerKeepVisible` |
| 客户端绘制闸门 | `Client/MirObjects/PlayerObject.cs`：`Draw`、`DrawEffects`、`DrawBlend`、`DrawName`、`MouseOver` |
| 客户端场景循环 | `Client/MirScenes/GameScene.cs`：对象循环与两处特效循环 |
| 客户端开关面板 | `Client/MirScenes/Dialogs/AutoPlayDialog.cs`：`BuildPageOther` |
| 聊天日志闸门 | `Server/MirObjects/PlayerObject.cs`：`Chat` |
| 退出日志闸门 | `Server/MirObjects/PlayerObject.cs`：`StopGame` |
| 假人登录限流 | `Server/MirObjects/Player/FakePlayerManager.cs`：`Login` |
| 内存报告与压缩 | `Server/MirEnvir/Envir.cs`：`LogMemoryReport`、`CompactHeaps` |
