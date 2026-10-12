# 自定义 BUFF 属性系统 —— 使用说明

> 2026-10-12 新增。让 BUFF 的属性加成（AC/DC/MC/SC、HP/MP、暴击、吸血、经验、负重、恢复…）
> 从「写死在代码里」变成「配置文件可改」，并支持新建自定义 BUFF。

---

## 一、三个部件

| 部件 | 位置 | 作用 |
|---|---|---|
| 配置文件 | `Configs\BuffSystem.ini` | 存放所有 BUFF 的名称/图标/属性定义 |
| GM 编辑器 | 服务端本体（GM 工具）→ 菜单「数据库」→ **自定义BUFF属性** | 图形化增删改，写到上面的文件 |
| 运行引擎 | 服务端 `Server.Library.dll` | 启动读配置；施放 BUFF 时按配置算属性 |

---

## 二、怎么用（最短路径）

1. 打开服务端程序 → 菜单「数据库」→「自定义BUFF属性」
2. 左侧点 **「新建自定义」** → 自动分配一个槽位（1~24）
3. 右侧填 **名称**（客户端 BUFF 提示框标题）、**图标**、**是否可见**、**堆叠方式**
4. 在「属性加成」表格里点 **「添加属性行」**，选属性、填数值：
   - **基础值**：任何等级都加的值
   - **每级递增**：施法者每升 1 级额外增加的值
   - 最终加成 = `基础值 + 每级递增 × 等级`
5. 点 **「保存并生效」**
   - 服务器正在运行 → 立刻生效（不用重启）
   - 服务器没开 → 下次启动自动读

进游戏后用 `@GIVEBUFF <名字> <秒数>` 自己试效果。

---

## 三、配置文件格式

```ini
; 节名两种写法：
;   数字 1~24   = 自定义 BUFF（对应 BuffType.Custom1~Custom24）
;   已有BUFF名  = 给现有 BUFF 追加属性（如 [隐身术]）

[1]
Name=羽化登仙
Icon=30034
Visible=True
Stack=ResetDuration
Property=None
Desc=每级提升防御与攻击
Stat.MaxAC=5+2
Stat.HP=100+50

; 给已有 BUFF 加属性（去掉行首分号即可）
;[隐身术]
;Stat.攻击速度=3
```

### 字段说明

| 字段 | 说明 |
|---|---|
| `Name` | 显示名。为空时客户端回退显示枚举名 |
| `Icon` | 图标编码，见下表 |
| `Visible` | 是否对其他玩家可见（默认 True） |
| `Stack` | 堆叠方式，见下 |
| `Property` | 标记，多个用 `|` 连接 |
| `Desc` | 说明文本（编辑器里存档用） |
| `Stat.属性=数值` | 数值写 `基础` 或 `基础+每级` |

### 图标编码

| 值 | 图库 |
|---|---|
| `0` | 用原有硬编码映射（老 BUFF 全部走这条） |
| `10000+N` | `Data\Prguse` 第 N 张 |
| `20000+N` | `Data\Prguse2` 第 N 张 |
| `30000+N` | `Data\MagIcon` 第 N 张 |
| `40000+N` | `Data\MagIcon2` 第 N 张 |

> 举例：想复用「幽灵盾」的图标，它的编码是 `30026`，直接填 `30026` 即可。

### 堆叠方式（Stack）

`None` / `ResetDuration`（重置时间，最常用）/ `StackDuration`（累加时间）/
`StackStat`（叠加属性）/ `StackStatAndDuration` / `Infinite`（永久）/
`ResetStat` / `ResetStatAndDuration` / `AttrStackStat` / `AttrStackStatAndDuration`

### 标记（Property）

`None` / `RemoveOnDeath`（死亡移除）/ `RemoveOnExit`（下线移除）/
`Debuff`（负面状态）/ `PauseInSafeZone`（安全区暂停计时）

### 可用属性（Stat）

基础：`MinAC` `MaxAC` `MinMAC` `MaxMAC` `MinDC` `MaxDC` `MinMC` `MaxMC` `MinSC` `MaxSC`
`HP` `MP` `准确` `敏捷` `攻击速度` `幸运` `背包负重` `腕力负重` `装备负重`
`反弹伤害` `强度` `神圣` `冰冻伤害` `毒素伤害`

躲避与恢复：`魔法躲避` `毒物躲避` `生命恢复` `法力恢复` `中毒恢复`

暴击：`暴击倍率` `暴击伤害`

百分比类：`最大防御数率` `最大魔御数率` `最大物理攻击数率` `最大魔法攻击数率` `最大道术攻击数率`
`攻击速度数率` `生命值数率` `法力值数率` `吸血数率`

收益类：`经验增长数率` `物品掉落数率` `金币收益数率` `采矿出矿数率` `宝石成功数率`
`钓鱼成功数率` `大师概率数率` `技能熟练度倍率` `攻击增伤`

> ⚠ 带「数率」的属性单位是 **百分比**，例如 `经验增长数率=20` 表示 +20% 经验。
> ⚠ 数值可填负数（`Stat.MaxAC=-5` 就是减防御）。

---

## 四、游戏里怎么把 BUFF 发给玩家（三种方式）

### 方式 1：GM 命令（自己测试最快）

| 命令 | 说明 |
|---|---|
| `@GIVEBUFF <名字\|槽位> [秒数] [玩家名]` | 加 BUFF。例：`@GIVEBUFF 羽化登仙 300`、`@GIVEBUFF 1 600 某某玩家` |
| `@RELOADBUFF` | 改完 ini 后重新读取配置（不用重启） |
| `@CUSTOMBUFF` | 列出已配置的自定义 BUFF |

名字可以写：**显示名**（羽化登仙）、**槽位号**（1）、或**枚举名**（Custom1）。

### 方式 2：NPC 脚本（正式玩法用这个）

脚本系统本来就有 BUFF 动作，直接可用：

```
[@MAIN]
#IF
CHECKBUFF 羽化登仙
#SAY
你身上已经有这个增益了。
#ELSESAY
要领取「羽化登仙」吗？\\
<领取/@GET>
[~]
[@GET]
#ACT
GIVEBUFF 羽化登仙 1800 TRUE FALSE FALSE
SYSMSG "已获得羽化登仙"
```

动作/检查格式：

| 脚本命令 | 格式 |
|---|---|
| 加 BUFF | `GIVEBUFF <名字> <持续秒数> <是否永久> <是否可见> <是否可叠加>` |
| 检查 | `CHECKBUFF <名字>` |
| 移除 | `REMOVEBUFF <枚举名>` |

- 参数示例：`GIVEBUFF 龙之特效 3600 TRUE FALSE FALSE`（3600 秒、非永久、不可见、不可叠加）
- ★ 名字写 **显示名 / 槽位号 / 枚举名** 都行（这次专门做了兼容）。
- ★ **属性写在 `BuffSystem.ini` 里，不要写进脚本**。脚本如果要额外带属性（`... MaxDC=12` 这种），
  原版实现要求该 BUFF 在 `Envir\SetBuffs.txt` 里有登记行，否则会被静默丢弃 —— 这是个老坑，
  用新系统就不必碰它了。
- 改完脚本在游戏里打一次 `@RELOADNPCS` 即可，不用重启。

### 方式 3：脚本里按条件自动发

比如进地图、升级、每日触发时给：

```
[@_MAPENTER(InDun_01BossRoom)]
#IF
CHECKBUFF 羽化登仙
#ACT
BREAK
#ELSESAY
...
#ACT
GIVEBUFF 羽化登仙 3600 TRUE FALSE FALSE
```

（`[@_LOGIN] [@_LEVELUP] [@_MAPENTER(图)] [@_DAILY]` 等都是现成的触发钩子。）

---

## 五、必须知道的限制

1. **等级取「施法者」的等级**。GM 给自己加就按自己等级算，给玩家加就按那个玩家的等级算。
2. **给已有 BUFF 加属性只在第一次挂上时生效一次**。这些 BUFF 多是「重置时间」堆叠，
   每次重放都追加会越滚越大，所以刻意只加一次。重新生效需要先掉再上。
3. **改动 C# 代码后必须重启**；改配置文件不用重启，`@RELOADBUFF` 或编辑器保存即可。
4. `@RELOADBUFF` 会**清掉在线玩家身上所有自定义槽位 BUFF**（它们的内容完全来自配置），
   需要重新加。已有 BUFF 不受影响。
5. 自定义 BUFF 目前**只对玩家和英雄生效**（挂在 `HumanObject.AddBuff` 上），怪物身上加自定义
   BUFF 不会加属性。
6. `BuffType` 是 byte 枚举，自定义槽位开在 **230~253（24 个）**，不能无限加。
7. 服务端和客户端必须**同时更新**（本次改动加了 BUFF 封包字段）。只更新一边会掉线。

---

## 六、技术备注（给自己看的）

- 关键改动点：
  - `Shared/Enums.cs` —— `BuffType` 增加 `Custom1..Custom24`
  - `Shared/Data/ClientData.cs` —— `ClientBuff` 增加 `Name` / `Icon`
  - `Server/MirDatabase/BuffConfig.cs` —— 配置解析/保存（新增文件）
  - `Server/MirDatabase/BuffInfo.cs` —— 启动时合并配置、注册自定义槽位
  - `Server/MirObjects/HumanObject.cs` —— `AddBuff` 注入配置属性（`ApplyBuffConfigStats`）
  - `Server/MirObjects/PlayerObject.cs` —— `@RELOADBUFF` / `@GIVEBUFF` / `@CUSTOMBUFF`
  - `Server/MirEnvir/Envir.cs` —— `ReloadBuffConfig()`
  - `Client/MirScenes/Dialogs/BuffDialog.cs` —— 名称取 `buff.Name`、图标取 `buff.Icon`
  - `Server.MirForms/Database/BuffConfigForm.cs` —— GM 编辑器（新增文件）
- 属性累加早就在引擎里：`HumanObject.RefreshBuffs()` 会 `Stats.Add(buff.Stats)`，
  客户端提示框也会自动把 `buff.Stats` 列成「提高/降低 XXX」，所以**属性展示零改动**。
