# 项目长期记忆 —— Korean Mir2（Crystal C#/.NET8）

## 目录与产物
- 源码：`D:\mir2-20241027\`（`Server/`、`Client/`、`Shared/`、`Tools/`）
- 服务端运行目录：`E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\`
- 客户端运行目录：`E:\micromir2\`
- 服务端产物：直接编 `Server/Server.Library.csproj` → **`Server/bin/Release/Server.Library.dll`**（此工程没设 BaseOutputPath）。
  `Build/Server/Release/Server.Library.dll` 是 **Server.MirForms（Server.csproj）的输出副本**，两者都可用，部署时以刚编出来的那个为准。
- 客户端产物 `Build/Client/Release/Client.dll`（Client.csproj 设了 `BaseOutputPath=..\Build\Client\`）
- 部署时客户端要拷 **两处**：`E:\micromir2\` 和 `Mir2Server\ClientResources\`；部署后核对三处 MD5 一致
- ★★`Shared` 是**独立程序集**（`Shared.dll`），`Client` 与 `Server` 各输出一份同名 DLL，运行目录里**各自有一份**。
  **只要改了 `Shared/` 下的代码**（`ServerPackets.cs` / `Enums.cs` / `ItemData.cs` …），新 `Shared.dll` 必须同步部署到 **3 处**：
  `Mir2Server\Shared.dll`、`Mir2Server\ClientResources\Shared.dll`、`E:\micromir2\Shared.dll`。
  漏部署的后果：运行期 `System.MissingFieldException: Field not found: 'ServerPackets.XXX.YYY'`（2026-10-11 踩过）。
  产物位置：`Server/bin/Release/Shared.dll` 与 `Build/Client/Release/Shared.dll`（两份 MD5 相同，取任一份）。
- 备份命名：`Back Up\manual-YYYYMMDD_HHMM-描述\`；客户端 `Client.dll.YYYYMMDD_HHMM.bak`
- 独立微端资源服务：源码 `Tools\Mir2MicroServer\`，运行目录 `Mir2Server\MicroServer\`

## ★编译环境坑（反复踩）
- **大工程第二次 `dotnet build` 可能假死**（第一次正常，第二次卡在 CoreCompile 之后不退出）。
  处置：删掉工程下的 `obj`、`bin` 再编，或把源码拷到**全新目录**编译。
  ★2026-10-11 实测补充：`Server.Library.csproj` 只改一个文件后**直接增量编只花 5 秒、正常退出**；
  假死主要发生在**客户端**（大工程 + C 盘空间紧张）时。真假死时产物其实已生成 → 直接取 `obj/Release/*.dll` 或 `Build/Client/Release/Client.dll`。
  ★同一天还踩到：`rm -rf obj bin`（safe-delete 走回收站）本身就要 5 分钟以上，会把前台 Bash 超时打掉（SIGTERM）。
  要么放后台跑，要么干脆别删、直接增量编。
- **不要把 obj/bin 改名后留在工程目录内** —— SDK 只排除 `obj/`、`bin/`，
  `obj_stale_bak\...\AssemblyInfo.cs` 会被当源码编译，报 `CS0579 特性重复`。
- 客户端编译偶发「收尾假死」：日志已出 `Client -> ...Client.dll` 但 MSBuild 不退出。
  判断法：核对产物时间 + grep error，无错则停掉进程、手动取 `obj/Release/Client.dll` 部署。
- C 盘剩余空间过低（1~2G）会让编译从几分钟变成十几分钟卡死；先看 `df -h /c`。
- Bash 里 `rm -rf` 走 WorkBuddy safe-delete（回收站），大目录可能跑 2~3 分钟并触发前台超时 "SIGTERM"，不是真失败。

## 游戏机制要点
- `Effect.Blend` **默认 true（加色混合）**，是「特效太亮」的根因，必须显式 `Blend = false`。
- `Effect` 构造函数第 4 个参数是**总时长 ms**（帧间隔 = duration / 帧数）。
- `MapObject.CanFly(Point)` 只查地形，**忽略生物**。
- `ItemObject` / `SpellObject` 的 `IsAttackTarget` 直接抛异常 —— 遍历 `Cell.Objects` 必须先按类型过滤。
- 存在**两个 CompleteMagic**：`HumanObject.CompleteMagic`（data[0] = UserMagic）与 `Map.CompleteMagic`（data[0] = HumanObject）。
- `Server/MirEnvir/Map.cs` 技能 switch 里的 `show` 变量被多个 case 共用，删不得。
- 客户端 UI 显示层实时反推、每帧比对，比事件钩子更稳。
- 服务端（除 `Server/Utils/` 外）**完全不用 `Task.Run`/`async`/`ThreadPool`**；WorkLoop、怪物 AI 都是 `new Thread`。
- ★★**怪物名有两套**：`MonsterInfo.Name`（内部名，如 `雷帝2`，掉落文件 `Drops\雷帝2.txt` 用它）
  与 `MonsterInfo.GameName`（显示名 = `Regex.Replace(Name, @"[\d-]", "")`，**把数字和连字符全剥掉**，`雷帝2`→`雷帝`）。
  `MonsterObject.Name` 返回的是 **GameName**。全服 1231 个怪里 591 个（48%）两者不同。
- ★★因此 **`CHECKEXACTMON` 原版在本服对「带数字的怪名」恒为假**（校验按 `Info.Name`、计数按 `d.Name`=GameName，两边永远对不上；
  写 `雷帝2` 计数比不过、写 `雷帝` 校验过不了）。不含数字的怪名（如 `青神石魔`）不受影响。
  **已于 2026-10-11 修复**（`NPCSegment.cs` 三处 `CheckExactMon` 改用 `IsKnownMonsterName` + `MonsterNameMatches`，
  写内部名只数该种、写显示名数全部）→ `Server.Library.dll` = `72a2e1e6f541a73d7f59368e939bb68f`，已部署。
  替代写法（不改代码时）：`CHECKMON <运算符> <数量> <地图> [实例]`（用 `map.MonsterCount` 计数器，与怪名无关）。
- ★辅助开关「穿人」已拆成 **4 档独立开关**（服务器设置 → 其他选项页；`Setup.ini [Optional]`）：
  `EnableWalkThrough`(穿人=其他玩家/英雄，默认 true)、`EnableWalkThroughMonster`(穿怪，true)、
  `EnableWalkThroughNPC`(穿NPC，true)、`EnableWalkThroughWall`(穿城墙/城门，**默认 false**)。
  - 服务端判定唯一入口：`HumanObject.CanWalkThrough(MapObject)`（`Walk`/`Run` 都走它；`Run` 额外排除 Merchant）。
  - 「城门/城墙」= 覆写了 `MapObject.BlockWalkThrough => true` 的那些**怪物**类：
    `CastleGate`(含 `Gate` AI 950)、`CityGate`(AI 952)、`Wall`(AI 951)、`BlockingObject`(AI 950/951/952 门墙关闭时显形的挡格)。
    ★只改 `CastleGate` 会漏掉 `CityGate` 和 `Wall`（2026-10-11 第一版就是这么漏的，用户仍能穿城门）。
  - 标记经协议 `ObjectMonster.NoWalkThrough` 下发 → 客户端 `MonsterObject.NoWalkThrough` → `MapControl.CanWalkThroughObject()`。
  - 客户端预测必须与服务端逐条对齐，否则「本地能过、服务端拉回」，内挂会一直撞门。
  - `<Guard>`/`<Trainer>` 等**不是**城墙，别加这个标记（守卫可移动、稻草人是普通怪）。
  - 设置改完保存即生效（移动判定每次都读 `Settings.*`），**不用重启服务端**。
- ★客户端 `MapControl` 与 `GameScene` **同处 `Client/MirScenes/GameScene.cs`** 一个文件：
  在 `MapControl` 里访问 GameScene 的静态字段必须写 `GameScene.XXX` 前缀，漏了报 `CS0103`。
- ★协议包 `ObjectMonster` 在 `Shared/ServerPackets.cs` 里：**改它 = 改 Shared → 必须重编并部署 `Shared.dll` × 3**（见上），同时客户端、服务端两端的 DLL 都要更新，否则整包解析错位 / MissingFieldException。

## 微端资源服务
- 线上 `Setup.ini`：`EnableResourceService=True`、`HTTPIPAddress=http://0.0.0.0:5679/`、`ResourcePath=.\ClientResources\`、`ResourceMaxConcurrent=64`
- 客户端 `E:\micromir2\Mir2Config.ini` 的 `[MicroClient]`：`Host`、`Concurrency=2`、`RateLimit=0（不限速）`
- 接口契约：`GET /resource?f=相对路径`、`GET /resindex`、`GET /resinfo`
- 资源白名单：`data/ map/ sound/ directx/ localization/`（防拖走 Mir2Config.ini）

## NPC 脚本系统（`Envir\NPCs\*.txt`）
- 脚本不会自动扫描：**必须由数据库 `NPCInfo.FileName` 指向**才会加载（FileName 相对 `Envir\NPCs\`，可不带 `.txt`）。
- 改完生效：游戏内 GM 输入 `@RELOADNPCS`（最方便）或重启。
- 编码 `File.ReadAllLines()` → **UTF-8（无 BOM 亦可）**，GBK 会乱码；`;` 注释必须独占一行。
- 保留页名（服务端 `NPCScript.cs` 写死，**名字一个字不能改**，页面里不用写 `#ACT`，进页自动弹面板）：
  `[@BUY] [@SELL] [@BUYSELL] [@REPAIR] [@SREPAIR] [@REFINE] [@REFINECHECK] [@REFINECOLLECT]`
  `[@REPLACEWEDDINGRING] [@BUYBACK] [@STORAGE] [@CONSIGN] [@MARKET] [@CRAFT] [@CREATEGUILD]`
  `[@REQUESTWAR] [@SENDPARCEL] [@COLLECTPARCEL] [@AWAKENING] [@DISASSEMBLE] [@DOWNGRADE] [@RESET]`
  `[@PEARLBUY] [@BUYUSED] [@BUYNEW] [@BUYSELLNEW] [@CREATEHERO] [@MANAGEHERO]`
- 触发钩子：`[@_LOGIN] [@_LEVELUP] [@_USEITEM(Shape)] [@_MAPCOORD(图,X,Y)] [@_MAPENTER(图)] [@_DIE]`
  `[@_CUSTOMCOMMAND(名字)] [@_TRIGGER(参数)] [@_DAILY] [@_ONACCEPTQUEST(索引)] [@_ONFINISHQUEST(索引)]`
- 段是「各自求值、结果合并」不是 if/else，互斥要靠每段 `#ACT` 末尾 `BREAK`。
- `#SAY` 每个物理行 = 一句对话；行尾 `\` 是换行符。
- 完整手册：`D:\mir2-20241027\Back Up\NPC脚本命令与GM命令手册.md`（46 检查 + 92 动作 + 47 变量 + 71 GM）。
- NPC 数据可导入 GM 工具（Server.MirForms → NPC → 导入），行格式：
  `FileName,地图文件名,X,Y,名字,外观,Rate,大地图显示,大地图图标,可传送,攻城可见,MinLev,MaxLev,限时显示,HourStart,MinuteStart,HourEnd,MinuteEnd`

### ★★脚本容错（2026-10-11 加固，改动已上线）
- **崩溃链**：脚本异常 → `Envir.WorkLoop` 内层 `catch`（Envir.cs:764）→
  `Connections[i].SendDisconnect(3)`（**踢光所有在线玩家**）→ 继续 `StopNetwork()/StopEnvir()` → **整个服务器停摆**。
  也就是说：**改之前，任何一个脚本写错 = 全服掉线 + 停服**。
- 已加的 4 层护栏（都在 `Server/`，只需编 `Server.Library.csproj`）：
  1. `NPCSegment.Compare<T>` 非法运算符 **不再 throw**，返回 `false`（按条件不成立处理）。
  2. `ParseCheck` 解析期校验运算符位置并记可读日志（`WarnIfBadOperator`）——
     **写错只提示、不崩**，并且 `@RELOADNPCS` 是安全的。
  3. `NPCScript.ProcessSegment`(3 个重载) / `Response`(ProcessSpecial) / `CallSilent` / `Call` 全部 try/catch；
     玩家路径失败按「条件不成立」处理；`NPCSuccess` 改用**索引器**（原 `.Add()` 遇重复段会抛）；`player.NPCPage` 判空。
  4. `Envir.ReloadNPCs()` 逐个脚本 try/catch → 单个脚本坏只记 `NPC脚本加载失败（已跳过）`。
- ★比较运算符只有 `< > <= >= == !=` **六个**，**没有 `=`**（韩版脚本常写成 `=`，移植过来必崩，2026-10-11 用户就踩了）。
- ★`CHECK*` 命令里运算符的**位置**（写错位置 = 参数错位，条件恒不成立）：
  `LEVEL / CHECKGOLD / CHECKGUILDGOLD / CHECKCREDIT / CHECKPKPOINT / CHECKHUM / CHECKMON` → 第 2 个词；
  `CHECKEXACTMON / CHECKCALC` → 第 3 个词。
  正确写法举例：`CHECKEXACTMON 雷帝1 > 0 InDun_01BossRoom 1`、`CHECKHUM > 0 D10071`。
- ★`CHECKHUM` 语法：`CHECKHUM <运算符> <人数> <地图> [实例]`（`map.Players.Count()`）。
- ★**检测地图怪物数量**三条命令（`NPCSegment.cs` CheckMon/CheckExactMon 分支，玩家版 2313/2343、NPC版 1724/1752）：
  - `CHECKMON <运算符> <数量> <地图> [实例]` —— 整图怪物总数（`map.MonsterCount`）。
    ★玩家上下文里会**减掉自己宠物数** `player.Pets.Count()`（NPC/怪物上下文不减）。
  - `CHECKEXACTMON <怪名> <运算符> <数量> <地图> [实例]` —— 指定怪的**存活**数量（`!d.Dead`）。
    ★`<怪名>` **必须去掉空格**：服务端拿 `d.Name.Replace(" ","")` 跟脚本原样字符串比较（右侧不去空格），
    写成「雷帝 1」会永远判 0。另外 `<怪名>` 必须在怪物库存在，否则整条恒不成立。
  - 地图参数用 `MapInfo.FileName`（**不是** Title），可写变量 `<$MAP>` = 当前地图 FileName；`[实例]` 从 1 计，默认 1。
    ★本服每张图只有 **1 个 Map 实例**（`MapInfo.CreateMap()` 只 `new Map(this)` 一次，文件名 = `FileName + ".map"`），
    所以 `[实例]` 写 `0`/`1`/省略 都一样；写 `2` 会 `map == null` → 恒不成立。
  - 无「取数量并显示」的变量（变量表里只有 `<$USERCOUNT>` 全服在线人数），数量只能用于 `#IF` 判断。
- ★★实测案例（2026-10-11）：`NPCs\昆仑\燃烧昆仑\宗师石像1.txt` 写 `CHECKEXACTMON 雷帝1 > 0 InDun_01BossRoom 1` **恒不成立**，
  **不是语法错**，而是 `InDun_01BossRoom`（长老堂，MapIndex 519）配的刷怪是 **雷帝2**，`雷帝1` 全服**没有任何地图刷**。
  - 同名系列怪的属性可以差很远：雷帝1 HP=96000 / 雷帝2、雷帝3 HP=9600（AI 都是 508，DC/AC/EXP 一致）。
  - 掉落按怪名加载（`Envir.cs:3926` → `Drops\<怪物名>.txt`，除非 `MonsterInfo.DropPath` 另有指定），
    所以给「雷帝1」配的掉落对「雷帝2」**不生效**。
  - ★排查手段：`Tools\DbProbe`（从 `Tools\DbDump` 复制改造的**只读**探针，输出地图→刷怪表/怪名/HP/DropPath），
    用法：把 `bin\Release\net8.0\DbProbe.exe` 放到服务端目录（与 `Server.MirDB` 同级）运行。
  - ★诊断「脚本条件不生效」的正确顺序：① 命令语法/参数位置 → ② 脚本行的**原始字节**（全角空格 U+3000 / NBSP 会让 `Split(' ')` 拆错）→
    ③ 怪名/地图名在数据库里是否真实存在 → ④ **该地图是否真的刷这个怪**（最容易漏）→ ⑤ 改完是否 `@RELOADNPCS`。
- ★`NPCScript` 里那段「逐行 ParseCheck/ParseAct」的代码在 **`ParseSegment(NPCPage page, ...)`** 里，
  不在 `ParsePage` —— 报错信息要用 `page.Key`（写成 `sectionName` 会 CS0103）。

## 装备强化系统（服务端原生，共 3 套）
1. **武器精炼** `[@REFINE]`：`PlayerObject.RefineItem/CollectRefine/CheckRefine`，配置 `Configs\RefineSystem.ini`。
   武器 + 首饰 + `黑色铁矿石` → 扣 `武器等级要求 × 10 × RefineCost(125)` 金币 → `RefineTime(20)` 分钟后取回 → 手动「检查」才掷成功率（`BaseChance=20`，首饰/矿石/幸运加成），**失败武器破碎**，`CritChance=10` 暴击 ×`CritIncrease=2`。
   ★ `DepositRefineItem()` 会校验玩家身边有**真实 NPC**（`CurrentMap.NPCs` 里 ObjectID 匹配且 `Globals.DataRange` 内）——纯文本/自定义命令**挂不上**，必须有 NPC。
2. **装备觉醒** `[@AWAKENING]`：配置 `Configs\AwakeningSystem.ini`（`SuccessRate/HitRate/MaxUpgradeLevel=5/WeaponValue/HelmetValue/ArmorValue`）。
   6 种属性 ↔ `ItemType.觉醒物品` 的 `Shape`（0 攻击雕文/1 魔法雕文/2 道术雕文/3 防御雕文/4 魔御雕文/5 体力雕文）+ `Shape=100` 的 `觉醒之魂`，按装备 `Grade`（普通0/宝物1/圣物2/神物3）选同名后缀。每级材料数 = 1 + 当前觉醒等级。
3. **宝石/拆解/降级/重置**：`[@DISASSEMBLE] [@DOWNGRADE] [@RESET]`、`Configs\GemSystem.ini`。
- 全服目前**没有任何 NPC 开放过** `[@AWAKENING]/[@REFINECHECK]/[@REFINECOLLECT]`；仅 `Envir\NPCs\夔龙城\城内\制造尚宫-4.txt` 有个没接线的 `[@REFINE]`。
- 已交付脚本：`Envir\NPCs\武器强化师.txt`（精炼 + 觉醒），备份在 `WorkBuddy ...\outputs\武器强化师.txt`。
