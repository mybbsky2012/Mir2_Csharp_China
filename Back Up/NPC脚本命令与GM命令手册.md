# 传奇2 NPC 脚本命令与 GM 命令完整手册

> **适用服务端**：`D:\BaiduNetdiskDownload\20260915更新\20260915Mir2Server`
> **数据来源**：直接从服务端源码逐条提取核对，不是网上抄的通用文档。
> 提取自 `Server\MirObjects\NPC\NPCSegment.cs`（脚本命令）与 `Server\MirObjects\PlayerObject.cs`（GM 命令）。
>
> **收录数量**
> | 类别 | 数量 | 说明 |
> |---|---|---|
> | 检查命令（`#IF` 下用） | 46 条 | 判断条件 |
> | 动作命令（`#ACT` 下用） | 92 条 | 执行动作 |
> | 脚本变量（`#SAY` 里用） | 47 个 | 显示玩家/地图/行会数据 |
> | GM 命令（聊天框输入） | 71 条 | 以 `@` 开头 |
>
> ⚠️ 本手册以**你服务端的实际代码**为准。原版 `Envir\NPCs\NPC命令说明.txt` 是另一分支的文档，里面提到的 `BuyGT`、`HeroLevel`、`CheckMapLight`、`HASGT` 等命令**在你这个版本里并不存在**，不能照抄。

---

## 目录

- [一、脚本基础](#一脚本基础)
  - [1.1 脚本放哪、怎么被加载](#11-脚本放哪怎么被加载)
  - [1.2 文件格式要求](#12-文件格式要求)
  - [1.3 页面 \[@标签\]](#13-页面-标签)
  - [1.4 五个段指令](#14-五个段指令)
  - [1.5 关键执行顺序（很多人踩坑的地方）](#15-关键执行顺序很多人踩坑的地方)
  - [1.6 按钮写法](#16-按钮写法)
  - [1.7 变量与标志位](#17-变量与标志位)
  - [1.8 复用脚本 #INCLUDE 与 #INSERT](#18-复用脚本-include-与-insert)
  - [1.9 系统脚本与触发钩子](#19-系统脚本与触发钩子)
- [二、检查命令（46 条）](#二检查命令46-条)
- [三、动作命令（92 条）](#三动作命令92-条)
- [四、脚本变量（47 个）](#四脚本变量47-个)
- [五、GM 命令（71 条）](#五gm-命令71-条)
- [六、触发钩子对照表](#六触发钩子对照表)
- [七、完整示例](#七完整示例)
- [八、常见坑速查](#八常见坑速查)
- [附：数据核对方法](#附数据核对方法)

---

## 一、脚本基础

### 1.1 脚本放哪、怎么被加载

```
20260915Mir2Server\
└── Envir\
    ├── NPCs\                     ← NPC 脚本目录，可以任意建子目录
    │   ├── 00Default.txt         ← 系统脚本总入口（触发钩子）
    │   ├── 飞天县\
    │   │   └── 边界村\
    │   │       └── 边界仓库-0.txt
    │   └── 游戏活动\
    └── SystemScripts\00Default\  ← 系统脚本实体
        ├── Login.txt
        ├── LevelUp.txt
        └── 等级触发\
```

**关键点：脚本不会自动被扫描。**

一个脚本文件只有在**数据库里的 NPC 记录（`NPCInfo`）指向它**时才会被加载：

| NPCInfo 字段 | 含义 |
|---|---|
| `Name` | NPC 显示名（下划线 `_` 会显示成空格） |
| `MapIndex` | 所在地图 |
| `X` / `Y` | 所在坐标 |
| `FileName` | **脚本文件路径**（相对 `Envir\NPCs\`，不含扩展名或含 `.txt`） |
| `Image` | 外观编号 |

也就是说：**新建一个 `.txt` 文件放在 `Envir\NPCs\` 里，游戏里是看不到的**，必须在数据库里挂一个 NPC 指向它。

**改完脚本后的生效方式**（三选一）：
1. 游戏内 GM 输入 `@RELOADNPCS` —— 立即重载，最方便
2. 重启服务端
3. GM 工具里重载 NPC

### 1.2 文件格式要求

| 项目 | 要求 |
|---|---|
| 编码 | **UTF-8**（带 BOM 或不带都行）。用 GBK 保存会乱码 |
| 换行 | `CRLF`（Windows）或 `LF` 均可 |
| 注释 | **行首** `;` 开头的整行被忽略 |
| 大小写 | 命令名、标签名**不区分大小写** |
| 缩进 | 无要求，纯装饰 |

> `;` 注释必须独占一行。`#SAY` 文本里想要行内留言是不行的。

### 1.3 页面 `[@标签]`

一个脚本文件由若干**页面**组成，每页以 `[@标签]` 开头：

```
[@Main]
#SAY
你好，<$USERNAME>。有什么需要吗？\
<查看等级/@查看等级> <离开/@exit>

[@查看等级]
#SAY
你当前 <$LEVEL> 级。
<返回/@Main>
```

规则：

- `[@Main]` 是 NPC 的**默认入口页**，玩家点开 NPC 就进这里（大小写不敏感，`[@main]` 也行）
- 标签名随便取（中文、英文都行），只要和按钮里写的对得上
- 一个文件里可以有任意多个页面，理论上没有上限
- 页面顺序无所谓，`[@Main]` 不必放第一个

### 1.4 五个段指令

页面内部用 `#` 开头的指令划分**段**：

| 指令 | 作用 |
|---|---|
| `#IF` | 段开始；后面跟检查命令，**全部满足**才算通过 |
| `#SAY` | 条件通过时显示的文字（可带按钮） |
| `#ACT` | 条件通过时执行的动作 |
| `#ELSEACT` | 条件**不**通过时执行的动作 |
| `#ELSESAY` | 条件**不**通过时显示的文字 |

一个页面里可以写多个 `#IF` 段。`#SAY` / `#ACT` 的先后顺序无所谓，命令引擎会分别收集。

**标准写法（推荐顺序）：**

```
[@Main]
#IF
CHECKGOLD > 99999
#SAY
你身上的金币超过了 10 万。

#ACT
LOCALMESSAGE "有钱人！"
#ELSESAY
金币不够 10 万，继续努力。
```

**`#IF` 多行条件是 AND（全部满足才通过）：**

```
#IF
LEVEL > 30
CHECKCLASS 战士
CHECKITEM 屠龙 1
#SAY
勇士，你可以领取屠龙刀了。
```

以上三条必须同时成立。

**支持的比较操作符**（用于 `LEVEL`、`CHECKGOLD`、`CHECKCALC`、`RANDOM` 等带操作符的命令）：

| 符号 | 含义 |
|---|---|
| `>` | 大于 |
| `<` | 小于 |
| `>=` | 大于等于 |
| `<=` | 小于等于 |
| `=` | 等于 |
| `!=` | 不等于 |

> ⚠️ 很多命令是 `命令 <操作符> <值>`，操作符写中间，别把顺序写反（写成 `LEVEL 30 >` 是错的）。

### 1.5 关键执行顺序（很多人踩坑的地方）

这是本服务端脚本引擎的真实行为，务必理解：

1. 进入一个页面后，**页面里所有 `#IF` 段都会被依次求值**，一个都不跳过
2. 每段求值结果：
   - 通过 → 执行该段 `#ACT` + 把 `#SAY` 加进显示队列
   - 不通过 → 执行该段 `#ELSEACT` + 把 `#ELSESAY` 加进显示队列
3. 所有段的结果**叠加**在一起显示

**这意味着多个段不是 `if / else if` 关系，而是"各自独立判断、结果合并"。**

如果你想要"互斥分支"效果，必须在每段 `#ACT` 末尾加 `BREAK`：

```
[@领奖]
#IF
CHECKITEM 屠龙 1
#SAY
你已经领过了。
#ACT
BREAK           ← 中断后续所有段

#IF
LEVEL > 30
#SAY
恭喜领取屠龙刀！
#ACT
GIVEITEM 屠龙 1
BREAK
```

**另一个后果**：不写 `#IF` 的 `#SAY` 会**无条件显示**。所以"默认页"通常直接写 `#SAY`，不带条件。

### 1.6 按钮写法

按钮写在 `#SAY`（或 `#ACT` / `#ELSESAY`）的文字里，格式：

```
<显示文字/@目标标签>
```

- `显示文字` 是玩家看到的文字（可以用颜色代码、可以用变量）
- `@目标标签` 指向本脚本（或 `#INCLUDE` 进来的脚本）中的某一页
- 标签可以省略 `[]`，写成 `@Main` 就行

**多按钮排在一行：**

```
#SAY
请选择：\
<传送服务/@传送> <仓库/@仓库> <离开/@exit>
```

**特殊目标标签：**

| 写法 | 作用 |
|---|---|
| `@exit` | 关闭对话框（引擎内置） |
| `@Main` | 回主页面（需你自己定义 `[@Main]`） |

**`#ACT` 里跳转也会生成按钮：**

`GOTO`、`GROUPGOTO`、`TIMERECALL`、`TIMERECALLGROUP`、`DELAYGOTO` 写在 `#ACT` 里时，引擎会自动把跳转目标登记成按钮（因为静默执行时玩家还需要能跳过去）。

**关于 `\` 换行**：`#SAY` 文本里行尾的 `\` 是客户端换行符，用于长文本折行。

### 1.7 变量与标志位

**三种"存储"机制：**

| 机制 | 写法 | 说明 |
|---|---|---|
| 计数器变量 | `A0` ~ `Z9`，引用时写 `%A1` | 单个字母+单个数字，共 260 个槽位 |
| 布尔标志位 | `[100]`，引用时写 `\$标志` | 玩家身上的持久化标志数组，用于"是否已完成某事" |
| 临时参数 | `%ARG(0)` | NPC 被调用时传入的参数 |

**存值与运算：**

```
#ACT
MOV A1 100              ; 把 100 存进 A1
CALC A1 + 10            ; A1 = A1 + 10  → 110
CALC A1 * 2             ; 110 * 2 → 220
```

**判断变量：**

```
#IF
CHECKCALC %A1 > 100     ; 判断 A1 是否大于 100
```

**设置标志位：**

```
#ACT
SET [100] 1             ; 把第 100 号标志位置为真
```

**判断标志位：**

```
#IF
CHECK [100] 1           ; 第 100 号标志位是否为真
```

> 标志位索引是 `[方括号]` 包起来的，不能省。索引上限见 `Globals.FlagIndexCount`。

**变量在 `#SAY` 里显示**：用 `<$变量名>`，详见 [第四章](#四脚本变量47-个)。

### 1.8 复用脚本 #INCLUDE 与 #INSERT

两个指令长得像，作用完全不同：

#### `#INCLUDE 文件名 @标签` —— 只取指定页

把另一个脚本文件的**某一个页面**内容搬进来。用于把公共页面（如全服通用的传送菜单）集中管理。

```
[@Main]
#SAY
<查看公告/@公告>

#INCLUDE [SystemScripts\00Default\等级触发\新人公会.txt] @Main
```

- 路径相对 `Envir\`，用方括号包起来（**可选**，但推荐）
- 只复制 `[@Main]` 那个页面，其它页不要
- 被 include 的内容会展开到 **`#INCLUDE` 所在页面的开头**

#### `#INSERT 文件名 @标签` —— 取 `{}` 块整块内容

把另一个文件里 `[@标签] { ... }` 花括号之间的**所有行**搬进来。

```
#INSERT [SystemScripts\00Default\Login.txt] @Main
```

对应的目标文件必须长这样：

```
[@Main]
{
#IF
...
#ACT
...
}
```

- 花括号 `{` `}` 必须各占一行
- 两个指令都支持多层嵌套展开

### 1.9 系统脚本与触发钩子

`Envir\NPCs\00Default.txt` 是**全局系统脚本入口**，它本身只有一行行的 `#INSERT`：

```
#INSERT [SystemScripts\00Default\Login.txt] @Main
#INSERT [SystemScripts\00Default\LevelUp.txt] @Main
#INSERT [SystemScripts\00Default\UseItems.txt] @Main
#INSERT [SystemScripts\00Default\MapCoords.txt] @Main
#INSERT [SystemScripts\00Default\MapEnter.txt] @Main
#INSERT [SystemScripts\00Default\MapLeave.txt] @Main
#INSERT [SystemScripts\00Default\Die.txt] @Main
#INSERT [SystemScripts\00Default\Trigger.txt] @Main
#INSERT [SystemScripts\00Default\CustomCommands.txt] @Main
#INSERT [SystemScripts\00Default\OnAcceptQuests.txt] @Main
#INSERT [SystemScripts\00Default\OnFinishQuests.txt] @Main
#INSERT [SystemScripts\00Default\Daily.txt] @Main
#INSERT [SystemScripts\00Default\Client.txt] @Main
#INSERT [SystemScripts\00Default\HeroUseItems.txt] @Main
```

每个文件里定义若干 `[@_事件名]` 页面，游戏事件触发时被调用。详见[第六章](#六触发钩子对照表)。

> 想在**玩家升级时发装备**、**登录时给提示**、**杀死某怪时触发剧情**，都是往这些文件里加页面。

---

## 二、检查命令（46 条）

写在 `#IF` 下面。写成 `命令 参数...` 的形式，一行一条，多行之间是 AND 关系。

---

### A. 身份与权限（4 条）

#### `ISADMIN`
判断玩家是否是**管理员**（GM）。

```
#IF
ISADMIN
#SAY
你好，管理员。
```

---

#### `CHECKNAMELIST <文件名>`
判断玩家**名字**是否在指定名单文件里。名单文件放在 `Envir\` 下，一行一个名字。

```
#IF
CHECKNAMELIST 白名单.txt
```

> 注意：**不能**用变量作为文件名。

---

#### `CHECKGUILDNAMELIST <文件名>`
判断玩家所属**行会名**是否在名单文件里。

```
#IF
CHECKGUILDNAMELIST 攻城行会.txt
```

---

#### `CHECKPERMISSION <权限名>`
判断玩家在行会中的**职位权限**。可选值（区分大小写宽松）：

| 权限名 | 含义 |
|---|---|
| `CanChangeRank` | 调整职位 |
| `CanRecruit` | 招收成员 |
| `CanKick` | 开除成员 |
| `CanStoreItem` | 存入行会仓库 |
| `CanRetrieveItem` | 取出行会仓库 |
| `CanAlterAlliance` | 改变联盟 |
| `CanChangeNotice` | 修改公告 |
| `CanActivateBuff` | 激活行会 buff |

```
#IF
CHECKPERMISSION CanChangeRank
#SAY
你是行会管理，可以调整职位。
```

---

### B. 等级 / 职业 / 性别（3 条）

#### `LEVEL <操作符> <数值>`
判断玩家**等级**。

```
#IF
LEVEL > 30
```

```
#IF
LEVEL = 45
```

---

#### `CHECKCLASS <职业名>`
判断玩家**职业**。可选值：

`战士` / `法师` / `道士` / `刺客` / `弓箭`

```
#IF
CHECKCLASS 战士
```

---

#### `CHECKGENDER <性别>`
判断玩家**性别**。可选值：`男性` / `女性`

```
#IF
CHECKGENDER 男性
```

> 发装备时特别有用——**盔甲类物品全部区分男女**，别发错。

---

### C. 货币与物品（5 条）

#### `CHECKGOLD <操作符> <数量>`
判断玩家**身上的金币**。

```
#IF
CHECKGOLD > 100000
```

---

#### `CHECKGUILDGOLD <操作符> <数量>`
判断玩家**所在行会的资金**。

```
#IF
CHECKGUILDGOLD >= 500000
```

---

#### `CHECKCREDIT <操作符> <数量>`
判断玩家的**信用点 / 元宝**（Credit）。

```
#IF
CHECKCREDIT > 100
```

---

#### `CHECKITEM <物品名> [数量] [耐久]`
判断玩家**背包里**是否有指定物品。

| 参数 | 必填 | 说明 |
|---|---|---|
| 物品名 | 是 | 与数据库中的物品名完全一致，含**空格**时用双引号包起来 |
| 数量 | 否 | 默认 `1` |
| 耐久 | 否 | 只统计耐久 ≥ 该值 **×1000** 的物品 |

```
#IF
CHECKITEM 金创药(小) 10
```

```
#IF
CHECKITEM "屠龙 强化版" 1
```

> ⚠️ 耐久参数按 **千分之一** 计算。写 `CHECKITEM 裁决之杖 1 5000` 表示要求耐久 ≥ 5,000,000（满耐久单位），平时别填。

---

#### `HASBAGSPACE <操作符> <空位数>`
判断玩家背包的**剩余空格数**。

```
#IF
HASBAGSPACE >= 9
#SAY
背包有位置，可以领取全套装备。
```

> 发大量装备前建议先判断，否则背包满时物品会**直接丢弃**。

---

### D. 地图与坐标（3 条）

#### `CHECKMAP <地图名>`
判断玩家是否在指定地图上。支持 `地图名-实例号` 格式。

```
#IF
CHECKMAP 0
```

```
#IF
CHECKMAP 沙巴克-1
```

| 常见地图文件名 | 说明 |
|---|---|
| `0` | 比奇省 |
| `1` | 沃玛森林 |
| `3` | 盟重省 |
| `D1001` | 沙巴克城内 |

> 地图名用的是**地图文件名**，不是中文名。到 `Envir\Maps\` 目录或 `MapInfo.txt` 里查。

---

#### `CHECKRANGE <X坐标> <Y坐标> <范围>`
判断玩家是否在指定坐标的**范围内**（以地图绝对坐标计算）。

```
#IF
CHECKRANGE 330 330 10
```

表示玩家是否在以 (330,330) 为中心、半径 10 格的范围内。

---

#### `CHECKHUM <操作符> <数量> <地图名> <实例号>`
判断**某张地图上的玩家数量**。

```
#IF
CHECKHUM > 10 沙巴克-1 1
```

> 常用于"人多就分流"或"活动地图人数限制"。

---

### E. 怪物（2 条）

#### `CHECKMON <操作符> <数量> <地图名> <实例号>`
判断**某张地图上的怪物总数**。

```
#IF
CHECKMON = 0 0 1
#SAY
比奇省的怪物已被清空！
```

---

#### `CHECKEXACTMON <怪物名> <操作符> <数量> <地图名> <实例号>`
判断**某张地图上指定怪物**的数量。

```
#IF
CHECKEXACTMON 沃玛教主 = 0 D2021 1
```

> 常用于"BOSS 是否已被击杀"判断。

---

### F. 队伍与宠物（6 条）

#### `GROUPLEADER`
判断玩家是否**是队长**（且正在组队）。

```
#IF
GROUPLEADER
```

---

#### `GROUPCOUNT <操作符> <人数>`
判断**队伍人数**。

```
#IF
GROUPCOUNT >= 3
#SAY
队伍人数达标，可以进入。
```

---

#### `GROUPCHECKNEARBY`
判断**队伍成员是否都在附近**（用于传送前检查）。

```
#IF
GROUPLEADER
GROUPCHECKNEARBY
#ACT
GROUPRECALL
```

---

#### `PETCOUNT <操作符> <数量>`
判断玩家**当前宠物数量**。

```
#IF
PETCOUNT = 0
```

---

#### `PETLEVEL <操作符> <等级>`
判断玩家**宠物的等级**。

```
#IF
PETLEVEL >= 7
```

---

#### `CHECKPET <宠物名>`
判断玩家**是否拥有指定名字的宠物**。

```
#IF
CHECKPET 神兽
```

---

### G. 标志位 / 运算 / 随机 / 状态（5 条）

#### `CHECK [标志索引] <0 或 1>`
判断玩家身上的**布尔标志位**。

```
#IF
CHECK [100] 1
#SAY
你已经完成过新手引导。
```

> 标志索引必须用 `[ ]` 包起来。配合 `SET [100] 1` 使用，是最常用的"是否已领过奖励"方案。

---

#### `CHECKCALC <左值> <操作符> <右值>`
通用的**数值比较**，支持变量和 `%ARG(n)`。

```
#IF
CHECKCALC %A1 >= 100
```

```
#IF
CHECKCALC %A1 < %A2
```

> 支持的操作符：`>` `<` `>=` `<=` `=` `!=`。这是做计数器逻辑的核心命令。

---

#### `RANDOM <1-100>`
**按百分比随机**，数字越大越容易通过。

```
#IF
RANDOM 30
#SAY
你触发了 30% 几率的隐藏事件！
```

| 写法 | 含义 |
|---|---|
| `RANDOM 100` | 必定通过 |
| `RANDOM 50` | 一半概率 |
| `RANDOM 1` | 1% 概率 |

---

#### `CHECKBUFF <Buff名>`
判断玩家**身上是否有指定 buff**。

```
#IF
CHECKBUFF 攻击加速
```

> Buff 名要与 `Envir\SetBuffs.txt` 及 `BuffType` 枚举中的名字一致。

---

#### `CHECKTRANSFORM <外形编号>`
判断玩家**当前变身外形的编号**。

```
#IF
CHECKTRANSFORM 0
#SAY
你现在是正常模样。
```

---

### H. 行会与攻城（8 条）

#### `INGUILD [行会名]`
判断玩家**是否属于某个行会**。带行会名则判断是否属于该行会。

```
#IF
INGUILD
```

```
#IF
INGUILD 天下会
```

---

#### `CHECKCONQUEST <攻城战索引>`
判断指定**攻城战是否已开启 / 存在**。

```
#IF
CHECKCONQUEST 0
```

---

#### `CONQUESTAVAILABLE <攻城战索引>`
判断该攻城战**当前是否可以发起**（有行会且无人正在进攻）。

```
#IF
CONQUESTAVAILABLE 0
```

---

#### `CONQUESTOWNER <攻城战索引>`
判断玩家**所在行会是否是该攻城战的城主**。

```
#IF
CONQUESTOWNER 0
#SAY
你们是沙巴克的主人！
```

---

#### `AFFORDGUARD <攻城战索引> <弓箭手索引>`
判断行会**是否付得起**修理这个弓箭手的费用。

#### `AFFORDGATE <攻城战索引> <城门索引>`
判断行会是否付得起修复城门的费用。

#### `AFFORDWALL <攻城战索引> <城墙索引>`
判断行会是否付得起修复城墙的费用。

#### `AFFORDSIEGE <攻城战索引> <器械索引>`
判断行会是否付得起修复攻城器械的费用。

```
#IF
AFFORDGATE 0 1
#SAY
金币足够修复城门。
```

> 这四条一般配合攻城战 NPC 使用，普通服可以忽略。

---

### I. 时间（3 条）

#### `DAYOFWEEK <英文星期>`
判断今天是星期几。**必须用英文**（代码里取 `DayOfWeek.ToString()`）。

| 取值 |
|---|
| `MONDAY` `TUESDAY` `WEDNESDAY` `THURSDAY` `FRIDAY` `SATURDAY` `SUNDAY` |

```
#IF
DAYOFWEEK SUNDAY
#SAY
今天是周日，双倍经验活动开启！
```

---

#### `HOUR <0-23>`
判断当前**小时**（服务器时间）。

```
#IF
HOUR = 20
#SAY
晚上 8 点，攻沙开始！
```

---

#### `MIN <0-59>`
判断当前**分钟**。

```
#IF
HOUR = 20
MIN < 30
#SAY
攻沙进行中。
```

---

### J. 任务与婚恋（4 条）

#### `CHECKQUEST <任务索引> <ACTIVE 或 COMPLETE>`
判断玩家任务状态。

```
#IF
CHECKQUEST 1001 ACTIVE
#SAY
你正在做这个任务。
```

```
#IF
CHECKQUEST 1001 COMPLETE
#SAY
你已经完成了。
```

| 第二参数 | 含义 |
|---|---|
| `ACTIVE` | 任务正在进行中 |
| `COMPLETE` | 任务已完成 |

---

#### `CHECKRELATIONSHIP`
判断玩家**是否已婚**（有伴侣关系）。

```
#IF
CHECKRELATIONSHIP
```

---

#### `CHECKWEDDINGRING`
判断玩家**是否佩戴结婚戒指**。

```
#IF
CHECKWEDDINGRING
```

---

#### `ISNEWHUMAN`
判断玩家是否是**该账号的第一个角色**（新手）。

```
#IF
ISNEWHUMAN
#SAY
欢迎新玩家！
```

> 实现上是判断"账号下角色数 > 1"的反面。

---

### K. 计时器（2 条，本服扩展）

#### `CHECKLOOPTIMER <定时器键>`
判断玩家的**循环定时器是否处于开启状态**。配合 `SETLOOPTIMER` 使用，是泡点功能的核心。

```
#IF
CHECKLOOPTIMER paodian
#SAY
泡点正在运行中。
#ELSEACT
SETLOOPTIMER paodian 1 paodian_reward
```

---

#### `CHECKTIMER <键> <秒数> <比较参数>`
判断定时器剩余秒数。

> ⚠️ **源码实现存在缺陷**：这个命令的键名与比较操作符共用了同一个参数位（`param[0]` 既当键名又当操作符），实际不可靠。
> **建议**：计时类逻辑统一用 `SETLOOPTIMER` + `CHECKLOOPTIMER`，或 `SETTIMER` + 页面内有 `MYSTERY` 判断的写法，别依赖 `CHECKTIMER`。

---

## 三、动作命令（92 条）

写在 `#ACT` 或 `#ELSEACT` 下面。语法 `命令 参数...`，一行一条，**按书写顺序依次执行**。

> 物品名/文本里含**空格**时，用双引号 `"..."` 包起来。
> 命令参数里可以直接写变量，例如 `GIVEGOLD %A1`、`GIVEITEM <$ARMOUR>`。

---

### A. 流程控制（3 条）

#### `BREAK`
**立即停止本页剩余段**的执行。

```
#ACT
BREAK
```

> 这是让"多段互斥"生效的关键。写在 `#ACT` 最后一行。

---

#### `GOTO <@标签>`
跳转到本脚本的另一页。

```
#ACT
GOTO @领奖成功
```

---

#### `CALL <脚本ID>`
调用指定脚本。

```
#ACT
CALL 12
```

> 参数是脚本的内部 ID，不是文件名。日常写脚本基本用不上，优先用 `GOTO` / `#INCLUDE`。

---

### B. 传送与场景（13 条）

#### `MOVE <地图名> [X] [Y]`
把玩家**传送到指定地图坐标**。

```
#ACT
MOVE 0 330 330
```

不写坐标或坐标填 `0 0` 时，会**在地图上随机落地**（半径 200 格内）。

```
#ACT
MOVE 3          ; 随机传送到盟重省
```

> 地图名用**地图文件名**（数字或字母编号），不是中文名。

---

#### `INSTANCEMOVE <地图名> <实例号> <X> <Y>`
传送到**指定地图副本实例**的坐标。适合做个人副本。

```
#ACT
INSTANCEMOVE D2001 1 50 50
```

---

#### `ENTERMAP`
进入当前 NPC 所在的**地图事件**（配合地图事件配置使用）。

```
#ACT
ENTERMAP
```

---

#### `TIMERECALL <秒数> [@目标页]`
**延时召回**：指定秒数后把玩家拉回 NPC 身边，并跳到指定页。

```
#ACT
TIMERECALL 30 @时间到
```

> 常用于"进入活动地图后 30 分钟自动传出"。

---

#### `TIMERECALLGROUP <秒数> [@目标页]`
同上，但作用于**整个队伍**。

```
#ACT
TIMERECALLGROUP 60 @回城
```

---

#### `BREAKTIMERECALL`
**取消**当前玩家的延时召回。

```
#ACT
BREAKTIMERECALL
```

---

#### `DELAYGOTO <秒数> <@目标页>`
延时后跳转到指定页（不移动玩家）。

```
#ACT
DELAYGOTO 5 @下一步
```

---

#### `GROUPRECALL`
把**队伍成员**全部召回到自己身边。

```
#IF
GROUPLEADER
#ACT
GROUPRECALL
```

---

#### `GROUPTELEPORT <地图名> [实例号] [X] [Y]`
把**整个队伍**传送到指定地图坐标。

```
#ACT
GROUPTELEPORT 3 1 330 330
```

---

#### `GROUPGOTO <@目标页>`
**全队**一起跳到指定页。

```
#ACT
GROUPGOTO @进入第二关
```

---

#### `CHANGEGENDER`
改变玩家**性别**（男↔女）。

```
#ACT
CHANGEGENDER
```

> 注意：性别变化会影响盔甲穿戴，慎用。

---

#### `CHANGECLASS <职业名>`
改变玩家**职业**。

```
#ACT
CHANGECLASS 法师
```

可选值：`战士` `法师` `道士` `刺客` `弓箭`

---

#### `CHANGEHAIR <发型编号>`
改变玩家**发型**。

```
#ACT
CHANGEHAIR 5
```

---

### C. 货币（8 条）

| 命令 | 作用 |
|---|---|
| `GIVEGOLD <数量>` | 给玩家**金币** |
| `TAKEGOLD <数量>` | 扣除玩家金币（不足时扣到 0） |
| `GIVEGUILDGOLD <数量>` | 给玩家所在**行会增加资金** |
| `TAKEGUILDGOLD <数量>` | 扣除行会资金 |
| `GIVECREDIT <数量>` | 给玩家**信用点/元宝** |
| `TAKECREDIT <数量>` | 扣除信用点 |
| `GIVEPEARLS <数量>` | 给玩家**珍珠** |
| `TAKEPEARLS <数量>` | 扣除珍珠 |

```
#ACT
GIVEGOLD 1000000
GIVECREDIT 100
```

> 给金币时若总量会溢出，会自动截断到上限，不会崩服。

---

### D. 物品（4 条）

#### `GIVEITEM <物品名> [数量]`
给玩家**物品**，放进背包。

```
#ACT
GIVEITEM 金创药(小) 10
GIVEITEM "屠龙 强化版" 1
```

> ⚠️ **背包满时物品会被直接丢掉**，不会掉在地上。发多件前先用 `HASBAGSPACE` 判断。

---

#### `TAKEITEM <物品名> <数量> [耐久]`
**扣除**玩家背包里的物品。

```
#ACT
TAKEITEM 金创药(小) 10
```

耐久参数用于只扣指定耐久的物品（同样按 ×1000 处理）。

---

#### `UNEQUIPITEM <装备部位>`
**强制脱下**玩家身上指定部位的装备。

```
#ACT
UNEQUIPITEM 武器
```

部位名与 `EquipmentSlot` 枚举一致（武器 / 盔甲 / 头盔 / 项链 / 手镯 / 戒指 / 腰带 / 靴子 / 照明物 等）。

---

#### `DROP <掉落文件>`
按**掉落配置文件**给玩家掉落物品。文件放在 `Envir\` 下。

```
#ACT
DROP 活动掉落.txt
```

---

### E. 经验、等级与 PK（9 条）

#### `GIVEEXP <经验值>`
给玩家**加经验**。

```
#ACT
GIVEEXP 10000
```

> 支持变量，例如泡点里写 `GIVEEXP %A1` 实现递增奖励。

---

#### `GIVEGUILDEXP <经验值>`
给玩家所在**行会增加经验**。

```
#ACT
GIVEGUILDEXP 5000
```

---

#### `CHANGELEVEL <等级>` 或 `CHANGELEVEL <+/-增减>`
**直接修改**玩家等级。带 `+` / `-` 时是相对增减。

```
#ACT
CHANGELEVEL 45
```

```
#ACT
CHANGELEVEL +1
```

> 改等级不会触发升级奖励脚本的发放逻辑，慎用于正式服。

---

#### `CANGAINEXP <0 或 1>`
**开关玩家的是否能获得经验**（1 = 可以，0 = 禁止）。

```
#ACT
CANGAINEXP 0
```

> 常用于挂机、观战、监禁等场景。

---

#### `GIVEHP <数值>`
立即**回复玩家 HP**。

```
#ACT
GIVEHP 500
```

#### `GIVEMP <数值>`
立即**回复玩家 MP**。

```
#ACT
GIVEMP 500
```

---

#### `SETPKPOINT <数值>`
**设置**玩家 PK 点数（直接赋值）。

```
#ACT
SETPKPOINT 0
```

#### `REDUCEPKPOINT <数值>`
**减少** PK 点数。

```
#ACT
REDUCEPKPOINT 100
```

#### `INCREASEPKPOINT <数值>`
**增加** PK 点数。

```
#ACT
INCREASEPKPOINT 100
```

> PK 点数为 0 且取回红名状态，常用于"洗红名"服务。

---

### F. 技能（2 条）

#### `GIVESKILL <技能英文名> [等级]`
**让玩家学会技能**。

```
#ACT
GIVESKILL Fencing 0
GIVESKILL FireBall 3
```

| 参数 | 说明 |
|---|---|
| 技能名 | **必须用英文 `Spell` 枚举名**（`Fencing`、`FireBall`、`Healing`…），不是中文名 |
| 等级 | `0` ~ `3`，对应技能的三级熟练度；默认 `0` |

常用技能枚举名：

| 职业 | 技能名（枚举） |
|---|---|
| 战士 | `Fencing` 基本剑术、`SpiritSword` 攻杀剑术、`Slaying` 刺杀剑术、`Thrusting` 破血狂杀 |
| 法师 | `FireBall` 火球术、`ThunderBolt` 雷电术、`Repulsion` 抗拒火环、`FireWall` 火墙、`FrostCrunch` 冰咆哮 |
| 道士 | `Healing` 治愈术、`PoisonDust` 施毒术、`SoulFireBall` 灵魂火符、`SummonSkeleton` 召唤骷髅 |
| 刺客 | `Haste` 疾风步、`FlashDash` 突刺、`MoonMist` 月影迷雾 |
| 弓箭 | `StraightShot` 直射、`DoubleShot` 双重射击、`ElementalShot` 元素射击 |

> 完整对照表见本服的技能表（`Tools\magic_dump.csv`，从数据库导出，含中文名↔枚举名）。

---

#### `REMOVESKILL <技能英文名>`
**删除**玩家技能。

```
#ACT
REMOVESKILL FireWall
```

---

### G. 宠物（3 条）

#### `GIVEPET <怪物名> [数量] [等级]`
**给玩家召唤宠物**。

```
#ACT
GIVEPET 骷髅 1 7
```

| 参数 | 上限 |
|---|---|
| 数量 | 最多 5 只 |
| 等级 | 最多 7 级 |

---

#### `REMOVEPET <宠物名>`
移除指定名字的宠物。

```
#ACT
REMOVEPET 神兽
```

---

#### `CLEARPETS`
**清空玩家所有宠物**。

```
#ACT
CLEARPETS
```

---

### H. 增益 Buff（3 条）

#### `GIVEBUFF <Buff名> <持续秒> <参数3> <参数4> <参数5> [额外属性...]`
给玩家**施加 buff**。Buff 的**数值效果**从 `Envir\SetBuffs.txt` 里按名字读取。

`SetBuffs.txt` 格式（分号分隔，末尾两个分号）：

```
破天的核心;生命值强化=50;法力值强化=50;MaxDC=100;幸运=1;;
```

```
#ACT
GIVEBUFF 破天的核心 600 true false false
```

| 参数位置 | 含义 | 常用值 |
|---|---|---|
| 1 | Buff 名 | 必须存在于 `SetBuffs.txt` |
| 2 | 持续**秒数** | `600` = 10 分钟 |
| 3 | 参数3 | `true` / `false` |
| 4 | 参数4 | `true` / `false` |
| 5 | 参数5 | `true` / `false` |

> ⚠️ 这个命令在源码里三个布尔参数**命名有交叉**（`visible` / `infinite` / `stackable` 的位置容易搞混），而且有 `SetBuffs.txt` 里同名条目时优先用文件数值。**建议直接照抄现成脚本里的写法改名字和秒数**，不要凭空组合布尔值。
> 更稳妥的替代方案：把 buff 做成「使用后加属性」的道具，让玩家自己吃。

---

#### `REMOVEBUFF <Buff名>`
移除玩家身上的 buff。

```
#ACT
REMOVEBUFF 破天的核心
```

---

#### `REFRESHEFFECTS`
刷新玩家身上的**装备特效 / 光效**显示。

```
#ACT
CHANGECLASS 战士
REFRESHEFFECTS
```

---

### I. 变量与标志位（3 条）

#### `SET [标志索引] <0 或 1>`
设置玩家的**布尔标志位**。

```
#ACT
SET [100] 1
```

> 配合 `CHECK [100] 1` 使用，是最标准的"记录玩家是否已完成某事"的方式（跨下线保留）。

---

#### `MOV <变量> <值>`
给**计数器变量**赋值。变量名格式：**1 个字母 + 1 位数字**。

```
#ACT
MOV A1 100
MOV A2 "你今天真帅"
```

可用的变量名：`A0`~`A9`、`B0`~`B9` …… `Z0`~`Z9`，共 260 个。

---

#### `CALC <变量> <操作符> <值>`
对变量做**四则运算**并写回变量。

```
#ACT
MOV A1 100
CALC A1 + 50      ; A1 = 150
CALC A1 * 2       ; A1 = 300
CALC A1 / 10      ; A1 = 30
CALC A1 - 5       ; A1 = 25
```

支持操作符：`+` `-` `*` `/`

> **泡点递增奖励**就是靠 `MOV` + `CALC` 实现的：
> ```
> #ACT
> CALC A1 + 1000
> GIVEEXP %A1
> ```

---

### J. 消息与表现（5 条）

#### `LOCALMESSAGE "<文本>" <聊天类型>`
给玩家发一条**个人提示**（只有自己看得到）。文本**必须用双引号**包起来。

```
#ACT
LOCALMESSAGE "你获得了 10000 点经验！" Hint
```

聊天类型（`ChatType` 枚举，常用）：

| 类型 | 效果 |
|---|---|
| `Normal` | 普通聊天框 |
| `System` | 系统提示（白色） |
| `Hint` | **屏幕中央提示**（最常用） |
| `Announcement` | 公告样式 |
| `LevelUp` | 升级提示样式 |
| `System2` | 次要系统提示 |
| `LineMessage` | 走马灯（横条滚动） |

> ⚠️ 第二个参数**不能省略**，否则命令无效。定时器静默执行（如泡点）时也靠它给玩家反馈。

---

#### `GLOBALMESSAGE "<文本>" <聊天类型>`
**全服广播**。语法与 `LOCALMESSAGE` 相同。

```
#ACT
GLOBALMESSAGE "玩家【<$USERNAME>】击败了沃玛教主！" Announcement
```

> ⚠️ 同样必须带聊天类型参数。

---

#### `PLAYSOUND <音效编号>`
在客户端**播放音效**。

```
#ACT
PLAYSOUND 1
```

---

#### `GETRANDOMTEXT <变量> <文件名>`
从指定文本文件里**随机取一行**，存进变量。文件放 `Envir\` 下。

```
#ACT
GETRANDOMTEXT A1 抽奖文本.txt
LOCALMESSAGE "恭喜你抽到了：%A1" Hint
```

---

#### `OPENBROWSER <网址>`
在客户端**弹出浏览器**打开网页。

```
#ACT
OPENBROWSER http://www.example.com
```

---

### K. 名单文件操作（6 条）

名单文件放在 `Envir\` 下，一行一个名字。常配合 `CHECKNAMELIST` / `CHECKGUILDNAMELIST` 做活动报名、白名单。

| 命令 | 作用 | 示例 |
|---|---|---|
| `ADDNAMELIST <文件名>` | 把**当前玩家名字**加进名单 | `ADDNAMELIST 报名名单.txt` |
| `DELNAMELIST <文件名>` | 从名单移除当前玩家 | `DELNAMELIST 报名名单.txt` |
| `CLEARNAMELIST <文件名>` | **清空**整个名单 | `CLEARNAMELIST 报名名单.txt` |
| `ADDGUILDNAMELIST <文件名>` | 把当前玩家**行会名**加入名单 | `ADDGUILDNAMELIST 攻城行会.txt` |
| `DELGUILDNAMELIST <文件名>` | 移除行会名 | `DELGUILDNAMELIST 攻城行会.txt` |
| `CLEARGUILDNAMELIST <文件名>` | 清空行会名单 | `CLEARGUILDNAMELIST 攻城行会.txt` |

典型的"活动报名"写法：

```
[@报名]
#IF
CHECKNAMELIST 报名名单.txt
#SAY
你已经报过名了。
#ELSEACT
ADDNAMELIST 报名名单.txt
#ELSESAY
报名成功！<返回/@Main>
```

---

### L. 定时器（3 条，本服扩展）

#### `SETTIMER <键> <秒数> <类型> [是否全局]`

设置一个**一次性倒计时**（到点触发）。`类型` 是 `byte` 数值，`是否全局` 填 `true` / `false`。

```
#ACT
SETTIMER MyTimer 60 0 false
```

- 不写第 4 参数 = 玩家个人计时器
- 写 `true` = 全服共享计时器（键名会加 `_-` 前缀）
- 秒数传负数会被归零

---

#### `SETLOOPTIMER <键> <间隔秒数> <@目标页>`
**★ 本服新增的循环定时器**，泡点功能的核心。按**玩家独立计时**，每隔 N 秒静默执行一次目标页，直到被 `EXPIRETIMER` 关停或玩家下线。

```
[@开始泡点]
#IF
CHECKLOOPTIMER paodian
#SAY
泡点已经在运行了。
#ELSEACT
SETLOOPTIMER paodian 1 paodian_reward
#ELSESAY
泡点已开启，每秒获得经验！\
<停止泡点/@停止泡点>
```

| 参数 | 说明 |
|---|---|
| 键 | 自定义名字，用于后续判断/关闭 |
| 间隔秒数 | 最小 `1`，填 0 或负数会被强制为 1 |
| 目标页 | 被定时调用的页面标签（不带 `@`） |

**目标页的写法要求：**

1. 必须写在本脚本文件里的**同一份**脚本内
2. 触发时是**静默执行**的 —— **不要写 `#SAY`**，玩家看不到对话框；用 `LOCALMESSAGE` 提示
3. 目标页**不需要**任何按钮指向它（引擎已修复动态页解析问题）

```
[@paodian_reward]
#ACT
GIVEEXP 10000
LOCALMESSAGE "泡点奖励：获得 10000 点经验" Hint
```

**关闭计时器：**

```
[@停止泡点]
#ACT
EXPIRETIMER paodian
LOCALMESSAGE "泡点已停止" Hint
```

**注意事项：**
- 玩家**下线会自动清除**全部循环定时器（不能离线泡点）
- 重复执行同名 `SETLOOPTIMER` 会**覆盖**旧定时器（相当于重置计时）
- 间隔别设太小（1 秒已经是合理下限），否则会拖累服务器

---

#### `EXPIRETIMER <键>`
**关闭**指定键的定时器（`SETTIMER` 或 `SETLOOPTIMER` 设的都行）。

```
#ACT
EXPIRETIMER paodian
```

---

### M. 地图参数与怪物（5 条）

#### `PARAM1 <地图名> [实例号]`
给**当前 NPC 页面**设置"目标地图"。后面 `MONGEN` 会刷在这张地图上。

#### `PARAM2 <X坐标>`
设置刷怪的 **X 坐标**。

#### `PARAM3 <Y坐标>`
设置刷怪的 **Y 坐标**。

这三条一般是**成组使用**的，用来指定"在哪里刷什么怪"：

```
[@刷怪]
#ACT
PARAM1 D2021 1
PARAM2 100
PARAM3 100
MONGEN 沃玛教主 1
```

---

#### `MONGEN <怪物名> [数量]`
在 `PARAM1/2/3` 指定的地图坐标**刷新怪物**。数量不写默认 `1`。

```
#ACT
MONGEN 沃玛教主 3
```

> 怪物名必须与数据库中的怪物信息名一致。

---

#### `MONCLEAR <地图名> [实例号] [怪物名]`
**清空**指定地图上的怪物（`Die()` 处理，会走死亡掉落逻辑）。

```
#ACT
MONCLEAR D2021 1
```

只清一种怪：

```
#ACT
MONCLEAR D2021 1 沃玛教主
```

---

### N. 存档变量（2 条）

用于**跨下线、跨角色**保存数据（比如"你上次抽奖抽到了什么"）。

#### `SAVEVALUE <值> <文件名> <组> <键>`
把值写入存档文件。

```
#ACT
SAVEVALUE "%A1" 抽奖记录.txt 本周 头奖
```

#### `LOADVALUE <变量> <文件名> <组> <键>`
从存档文件读回变量。

```
#ACT
LOADVALUE A1 抽奖记录.txt 本周 头奖
LOCALMESSAGE "上次抽到的是：%A1" Hint
```

> 参数顺序是：`<变量> <文件> <组> <键>`，与 `SAVEVALUE` 的值位置不同，注意别写反。

---

### O. 行会（2 条）

#### `ADDTOGUILD <行会名>`
把玩家**加入**指定行会。

```
#ACT
ADDTOGUILD 天下会
```

#### `REMOVEFROMGUILD`
把玩家从**当前行会移除**。

```
#ACT
REMOVEFROMGUILD
```

---

### P. 邮件（4 条）

发邮件的流程是：**先组邮件内容，再发送**。

#### `COMPOSEMAIL "<主题>" <聊天类型>`
开始**编辑一封邮件**，设置主题。文本必须带双引号，第二参数是 `ChatType` 枚举名。

```
#ACT
COMPOSEMAIL "系统奖励" System
```

#### `ADDMAILGOLD <金币数>`
给这封邮件**附加金币**。

```
#ACT
ADDMAILGOLD 100000
```

#### `ADDMAILITEM <物品名> <数量>`
给这封邮件**附加物品**。

```
#ACT
ADDMAILITEM 金创药(小) 20
```

#### `SENDMAIL`
**发送**邮件（必须放在最后）。

```
[@发奖]
#ACT
COMPOSEMAIL "活动奖励" System
ADDMAILGOLD 100000
ADDMAILITEM 金创药(小) 20
SENDMAIL
LOCALMESSAGE "奖励已发送到你的邮箱" Hint
```

> 邮件方式发物品的**好处**：不受背包空格限制，玩家自己取。发大量装备时优先用邮件。

---

### Q. 婚恋（2 条）

#### `MAKEWEDDINGRING`
为玩家**生成结婚戒指**（需满足婚恋系统条件）。

```
#ACT
MAKEWEDDINGRING
```

#### `FORCEDIVORCE`
**强制离婚**。

```
#ACT
FORCEDIVORCE
```

---

### R. 攻城战（10 条）

> 普通服基本用不到，除非你要做自定义攻城 NPC。

| 命令 | 语法 | 作用 |
|---|---|---|
| `CONQUESTGUARD` | `CONQUESTGUARD <攻城索引> <开/关>` | 开关弓箭手 |
| `CONQUESTGATE` | `CONQUESTGATE <攻城索引> <开/关>` | 开关城门 |
| `CONQUESTWALL` | `CONQUESTWALL <攻城索引> <开/关>` | 开关城墙 |
| `CONQUESTREPAIRALL` | `CONQUESTREPAIRALL <攻城索引>` | **修复**所有城墙/城门/守卫 |
| `TAKECONQUESTGOLD` | `TAKECONQUESTGOLD <数量>` | 扣攻城资金 |
| `SETCONQUESTRATE` | `SETCONQUESTRATE <攻城索引> <比率>` | 设置攻城的 NPC 比率 |
| `STARTCONQUEST` | `STARTCONQUEST <攻城索引>` | **立即开始**攻城战 |
| `SCHEDULECONQUEST` | `SCHEDULECONQUEST <攻城索引>` | 安排攻城战（按配置时间） |
| `OPENGATE` | `OPENGATE <地图名> <门编号>` | 开门 |
| `CLOSEGATE` | `CLOSEGATE <地图名> <门编号>` | 关门 |

```
#ACT
OPENGATE 3 1
```

---

### S. 骰子玩法（2 条）

#### `ROLLDIE <@结果页> <骰子数>`
掷骰子小游戏，结果跳到指定页。

```
#ACT
ROLLDIE @比大小 3
```

#### `ROLLYUT <@结果页> <数量>`
韩式掷柶（윷놀이）小游戏。

```
#ACT
ROLLYUT @柶戏结果 1
```

---

### T. 英雄（3 条）

#### `REVIVEHERO`
**复活**英雄。

```
#ACT
REVIVEHERO
```

#### `SEALHERO`
**封印**英雄（收起来）。

```
#ACT
SEALHERO
```

#### `DELETEHERO`
**删除**英雄。

```
#ACT
DELETEHERO
```

---

## 四、脚本变量（47 个）

写在 `#SAY` / `#ELSESAY` 的文字里，运行时自动替换成实际值。

**语法**：`<$变量名>`，带参数的写作 `<$变量名(参数)>`。

```
#SAY
你好，<$USERNAME>！\
你当前 <$LEVEL> 级 <$CLASS>，\
所在地图：<$MAPNAME>（<$X_COORD>,<$Y_COORD>）\
身上金币：<$GAMEGOLD>
```

---

### 4.1 玩家基础信息（7 个）

| 变量 | 说明 |
|---|---|
| `<$USERNAME>` | 玩家名字 |
| `<$LEVEL>` | 等级 |
| `<$CLASS>` | 职业（战士/法师/道士/刺客/弓箭） |
| `<$HP>` / `<$MAXHP>` | 当前 HP / 最大 HP |
| `<$MP>` / `<$MAXMP>` | 当前 MP / 最大 MP |

---

### 4.2 地图与位置（4 个）

| 变量 | 说明 |
|---|---|
| `<$MAP>` | 当前地图**文件名**（如 `0`、`D2021`） |
| `<$MAPNAME>` | 当前地图**中文名** |
| `<$X_COORD>` / `<$Y_COORD>` | 当前坐标 |

---

### 4.3 货币与 PK（3 个）

| 变量 | 说明 |
|---|---|
| `<$GAMEGOLD>` | 身上的金币 |
| `<$CREDIT>` | 信用点 / 元宝 |
| `<$PKPOINT>` | PK 点数 |

---

### 4.4 NPC 与环境（4 个）

| 变量 | 说明 |
|---|---|
| `<$NPCNAME>` | 当前对话框所属 **NPC 的名字**（名字里的 `_` 会显示成空格） |
| `<$DATE>` | 服务器当前日期（`2026/9/28` 格式） |
| `<$USERCOUNT>` | **全服在线人数** |
| `<$PARCELAMOUNT>` | 邮箱里**未领取**的邮件数量 |

---

### 4.5 身上装备（13 个）

显示玩家对应部位装备的名字，**没装备时显示「空」**。

| 变量 | 部位 |
|---|---|
| `<$WEAPON>` | 武器 |
| `<$ARMOUR>` | 盔甲（衣服） |
| `<$HELMET>` | 头盔 |
| `<$NECKLACE>` | 项链 |
| `<$RING_L>` / `<$RING_R>` | 左戒指 / 右戒指 |
| `<$BRACELET_L>` / `<$BRACELET_R>` | 左手镯 / 右手镯 |
| `<$BELT>` | 腰带 |
| `<$BOOTS>` | 靴子 |
| `<$AMULET>` | 护身符 |
| `<$STONE>` | 守护石 |
| `<$TORCH>` | 照明物 |

```
#SAY
你手上的武器是：<$WEAPON>
```

---

### 4.6 行会（4 个）

| 变量 | 说明 |
|---|---|
| `<$GUILDNAME>` | 所在行会名（未入会时显示「未入行会」） |
| `<$GUILDWARTIME>` | 行会战持续时间设置 |
| `<$GUILDWARFEE>` | 发动行会战需要消耗的金币 |
| `<$ROLLRESULT>` | 掷骰子结果（未掷过显示 `Not Rolled`） |

---

### 4.7 坐骑（2 个）

| 变量 | 说明 |
|---|---|
| `<$MOUNT>` | 坐骑名字（无坐骑显示 `No Mount`） |
| `<$MOUNTLOYALTY>` | 坐骑**忠诚度**（显示为当前耐久/最大耐久） |

---

### 4.8 带参数的变量（10 个）

这些必须带参数，格式 `<$变量名(参数)>`。

| 变量 | 参数 | 说明 |
|---|---|---|
| `<$OUTPUT(A1)>` | 变量名 | 输出**计数器变量**的值 |
| `<$MONSTERCOUNT(地图名)>` | 地图文件名 | 该地图上的**怪物数量** |
| `<$CONQUESTGUARD(索引,编号)>` | 攻城索引,弓箭手编号 | 弓箭手的状态与雇佣费用 |
| `<$CONQUESTGATE(索引,编号)>` | 攻城索引,城门编号 | 城门状态 |
| `<$CONQUESTWALL(索引,编号)>` | 攻城索引,城墙编号 | 城墙状态 |
| `<$CONQUESTSIEGE(索引,编号)>` | 攻城索引,器械编号 | 攻城器械状态 |
| `<$CONQUESTOWNER(索引)>` | 攻城索引 | 该攻城战的**城主行会名** |
| `<$CONQUESTGOLD(索引)>` | 攻城索引 | 攻城战资金 |
| `<$CONQUESTRATE(索引)>` | 攻城索引 | 攻城战 NPC 比率 |
| `<$CONQUESTSCHEDULE(索引)>` | 攻城索引 | 已安排的进攻行会名 / `No War Scheduled` |

**最常用的一个示例：**

```
#SAY
当前 A1 变量的值是：<$OUTPUT(A1)>
```

> 攻城类变量（`CONQUEST*`）只有做自定义攻城 NPC 时才需要，普通服可以忽略。

---

## 五、GM 命令（71 条）

GM 命令在**游戏聊天框**里输入，以 `@` 开头，命令名**不区分大小写**。

```
@LEVEL 45
@GIVEGOLD 1000000
@RELOADNPCS
```

---

### 5.0 怎样获得 GM 权限

有**两条路**：

#### 路线 1：管理员账号（推荐）

账号在数据库里被标记为**管理员账号**（`AccountInfo.AdminAccount = true`）时，登录即自动是 GM。

配置方式：
- 用 GM 工具在账号管理里勾选"管理员"
- 或用客户端内置的账号管理功能

#### 路线 2：管理员密码

普通账号在游戏里输入 `@LOGIN`，服务端会提示"请输入管理员密码"，接着在聊天框**直接输入密码**即可升级为 GM。

**你服务端的当前密码**（在 `Configs\Setup.ini` 里）：

```ini
[General]
GMPassword=@9396399
```

改密码：改这个文件后重启服务端。

> ⚠️ 密码用 `@` 开头，和 GM 命令前缀冲突，输入时要完整输入 `@9396399` 全部字符。

**关于权限范围的说明**：
- 少数命令要求**纯 GM**（`IsGM == true`），普通玩家即使有其他道具也用不了
- 另一些命令在 `Settings.TestServer = true`（测试服模式）时也开放给普通玩家
- 服务端会自动把 GM 角色**从排行榜剔除**，避免污染排名

---

### 5.1 权限与账号（1 条）

| 命令 | 语法 | 说明 |
|---|---|---|
| `@LOGIN` | `@LOGIN` | 进入 GM 登录流程，接着输入管理员密码 |

---

### 5.2 自身状态类（10 条）

| 命令 | 语法 | 说明 |
|---|---|---|
| `@SUPERMAN` | `@SUPERMAN` | **开关无敌模式**（同时开关 GM 不死） |
| `@GAMEMASTER` | `@GAMEMASTER` | **开关隐身**（GM 隐身，普通玩家看不到你） |
| `@OBSERVER` | `@OBSERVER` | **开关观察者模式**（隐身 + 不能交互） |
| `@CLEARBUFFS` | `@CLEARBUFFS` | 清除自己身上**所有 buff** |
| `@DIE` | `@DIE` | **自杀**（测试死亡触发用） |
| `@REVIVE` | `@REVIVE` / `@REVIVE <玩家名>` | **复活**自己或指定玩家并恢复满血 |
| `@RIDE` | `@RIDE` | **上/下坐骑** |
| `@TOGGLETRANSFORM` | `@TOGGLETRANSFORM` | **暂停/恢复变形外形效果** |
| `@SETLIGHT` | `@SETLIGHT <0-4>` | 设置自己的**视野亮度** |
| `@HAIR` | `@HAIR` / `@HAIR <编号>` | 改发型；不带参数时**随机**换一个 |

```
@SUPERMAN
@SETLIGHT 4
```

> `@REVIVE <玩家名>` 会在服务器公告里留下记录（`XX 被管理员 XX 复活`）。

---

### 5.3 传送与移动（11 条）

| 命令 | 语法 | 说明 |
|---|---|---|
| `@MAPMOVE` | `@MAPMOVE <地图名> [实例] [X] [Y]` | **移动到指定地图坐标** |
| `@GOTO` | `@GOTO <玩家名>` | **跳到指定玩家身边** |
| `@MAP` | `@MAP` | 显示**当前地图的中文名与地图 ID** |
| `@传送` | `@传送 <X> <Y>` | 在当前地图内**坐标传送**（消耗传送戒指时也走这里） |
| `@RECALL` | `@RECALL <玩家名>` | 把指定玩家**召唤到自己身边** |
| `@探测` | `@探测 <玩家名>` | **探测玩家位置**（非 GM 使用时冷却 180 秒） |
| `@OBSERVE` | `@OBSERVE <玩家名>` | **观察**指定玩家（旁观视角） |
| `@ALLOWOBSERVE` | `@ALLOWOBSERVE` | 开关"**允许别人观察我**" |
| `@经天纬地` | `@经天纬地` | **记忆传送**：队长发起全队召唤（需记忆套装） |
| `@天人合一` | `@天人合一` | **接受记忆传送**召回到队长身边 |
| `@心心相映` | `@心心相映` | **夫妻召唤**：把配偶召到自己身边（需戴结婚戒指） |

```
@MAPMOVE 0 330 330
@MAPMOVE 沙巴克-1
@GOTO 张三
```

> `@MAPMOVE` 支持 4 种参数形式：
> | 写法 | 效果 |
> |---|---|
> | `@MAPMOVE 地图名` | 在该地图**随机落地** |
> | `@MAPMOVE 地图名 实例` | 在指定实例随机落地 |
> | `@MAPMOVE 地图名 X Y` | 落到指定坐标 |
> | `@MAPMOVE 地图名 实例 X Y` | 指定实例 + 指定坐标 |

---

### 5.4 物品、货币与技能（11 条）

| 命令 | 语法 | 说明 |
|---|---|---|
| `@MAKE` | `@MAKE <物品名或编号> [数量]` | **制造物品**到背包 |
| `@GIVEGOLD` | `@GIVEGOLD <数量> [玩家名]` | 给**金币** |
| `@GIVEPEARLS` | `@GIVEPEARLS <数量> [玩家名]` | 给**珍珠** |
| `@GIVECREDIT` | `@GIVECREDIT <数量> [玩家名]` | 给**信用点/元宝** |
| `@GIVESKILL` | `@GIVESKILL <技能英文名> [等级] [玩家名]` | 学**技能** |
| `@DELETESKILL` | `@DELETESKILL <技能英文名> [玩家名]` | 删**技能** |
| `@CLEARBAG` | `@CLEARBAG [玩家名]` | **清空背包**（危险！） |
| `@ADDINVENTORY` | `@ADDINVENTORY` | **扩容背包** |
| `@ADDSTORAGE` | `@ADDSTORAGE` | **扩容仓库** |
| `@AWAKENING` | `@AWAKENING <觉醒类型> <等级>` | 给装备**附加觉醒属性** |
| `@REMOVEAWAKENING` | `@REMOVEAWAKENING` | **移除**装备觉醒 |

```
@MAKE 金创药(小) 50
@GIVEGOLD 1000000
@GIVESKILL FireBall 3
@GIVESKILL FireBall 3 张三
```

> 带 `[玩家名]` 参数的命令，不写就是**对自己**生效。
> `@CLEARBAG` 前务必确认目标，清空不可撤销。

---

### 5.5 等级、职业与属性（8 条）

| 命令 | 语法 | 说明 |
|---|---|---|
| `@LEVEL` | `@LEVEL <等级> [玩家名]` | 调整**等级** |
| `@LEVELHERO` | `@LEVELHERO <等级> [玩家名]` | 调整**英雄等级** |
| `@CHANGECLASS` | `@CHANGECLASS <职业> [玩家名]` | 改**职业** |
| `@CHANGEGENDER` | `@CHANGEGENDER [玩家名]` | 改**性别** |
| `@ADJUSTPKPOINT` | `@ADJUSTPKPOINT <数值> [玩家名]` | 调整 **PK 点数**（正数增加、负数减少） |
| `@SETFLAG` | `@SETFLAG <索引>` | **翻转**指定编号的标志位（开↔关） |
| `@LISTFLAGS` | `@LISTFLAGS` | 列出自己**所有已开启的标志位编号** |
| `@CLEARFLAGS` | `@CLEARFLAGS [玩家名]` | **清空所有标志位** |

```
@LEVEL 45
@CHANGECLASS 法师
@SETFLAG 100
@LISTFLAGS
```

> `@SETFLAG` 是**翻转**不是置 1，用之前先 `@LISTFLAGS` 看当前状态。
> `@CHANGECLASS` 参数用中文职业名。

---

### 5.6 怪物（4 条）

| 命令 | 语法 | 说明 |
|---|---|---|
| `@MOB` | `@MOB <怪物名或编号> [数量] [X] [Y]` | 在当前位置（或指定坐标）**刷怪** |
| `@RECALLMOB` | `@RECALLMOB <怪物名或编号> [数量] [等级]` | 把怪物**召唤为宠物**（数量上限 50，等级上限 7） |
| `@CLEARMOB` | `@CLEARMOB [怪物名]` | **清空**当前地图的怪物（可指定只清一种） |
| `@RELOADDROPS` | `@RELOADDROPS` | **重新加载掉落配置**（改完爆率不用重启） |

```
@MOB 沃玛教主 3
@MOB 1 10
@CLEARMOB
@RELOADDROPS
```

> 怪物参数支持**名字**或**编号**。用编号可能刷出名字显示不正常的怪物，优先用名字。
> `@MOB` **不能生成攻城类怪物**（弓箭手/城门/城墙），会提示"此命令不能生成攻城类怪物"。
> 非 GM 玩家用 `@RECALLMOB` 时会受宠物数量上限限制，GM 不受限。

---

### 5.7 服务端维护（6 条）

| 命令 | 语法 | 说明 |
|---|---|---|
| `@RELOADNPCS` | `@RELOADNPCS` | **重新加载全部 NPC 脚本**（改脚本后最常用） |
| `@CLEARIPBLOCKS` | `@CLEARIPBLOCKS` | **清除 IP 封禁列表** |
| `@BACKUPPLAYER` | `@BACKUPPLAYER <玩家名>` | **备份**指定玩家存档 |
| `@ARCHIVEPLAYER` | `@ARCHIVEPLAYER <玩家名>` | **归档**玩家（不能归档自己） |
| `@LOADPLAYER` | `@LOADPLAYER <玩家名>` | **加载**玩家存档 |
| `@RESTOREPLAYER` | `@RESTOREPLAYER <玩家名> [时间点]` | 从备份**恢复**玩家数据 |

```
@RELOADNPCS
@BACKUPPLAYER 张三
```

> **改完任何 `.txt` 脚本后，先 `@RELOADNPCS`，再测试。** 这是排查脚本问题第一步。

---

### 5.8 行会与攻城（10 条）

| 命令 | 语法 | 说明 |
|---|---|---|
| `@CREATEGUILD` | `@CREATEGUILD <行会名> [玩家名]` | **创建行会**（名字限 3-20 字符） |
| `@加入行会` | `@加入行会` | 开关"**允许玩家邀请入会**"（服务端全局开关） |
| `@退出行会` | `@退出行会` | 退出当前行会（行会战期间禁止） |
| `@STARTWAR` | `@STARTWAR <行会名>` | **发起行会战**（须是会长） |
| `@STARTCONQUEST` | `@STARTCONQUEST <攻城索引>` | **立即开始攻城战** |
| `@RESETCONQUEST` | `@RESETCONQUEST <攻城索引>` | **重置攻城战**（清空占领状态） |
| `@GATES` | `@GATES <OPEN\|CLOSE>` | 开关**城门**（需行会权限、非战时） |
| `@CHANGEFLAG` | `@CHANGEFLAG <旗子编号>` | 更换**行会旗帜** |
| `@CHANGEFLAGCOLOUR` | `@CHANGEFLAGCOLOUR <颜色参数...>` | 更换**旗帜颜色** |
| `@允许交易` | `@允许交易` | 开关"**允许与玩家交易**" |

```
@CREATEGUILD 天下会
@STARTWAR 敌对行会
@STARTCONQUEST 0
```

> `@加入行会` 和 `@允许交易` 是**开关型**命令，每输入一次状态翻转。

---

### 5.9 信息、任务与杂项（9 条）

| 命令 | 语法 | 说明 |
|---|---|---|
| `@INFO` | `@INFO` / `@INFO <玩家名>` | 查看**面前对象或指定玩家的详细信息** |
| `@TIME` | `@TIME` | 显示**服务器当前时间** |
| `@ROLL` | `@ROLL` | 在**队伍里掷骰子**（1-6） |
| `@TRIGGER` | `@TRIGGER <触发参数> [玩家名]` | **手动触发** NPC 脚本的 `[@_Trigger(...)]` 事件 |
| `@SETQUEST` | `@SETQUEST <任务索引> <状态码> [玩家名]` | **设置任务状态**（`0`=清除，`1`=标记完成） |
| `@CLEARQUESTS` | `@CLEARQUESTS [玩家名]` | **清空所有任务** |
| `@SUMMONHERO` | `@SUMMONHERO` | **召唤/收回英雄** |
| `@DECO` | `@DECO <外观编号>` | 在当前位置放置一个**装饰物**（纯视觉） |
| `@SETTIMER` | `@SETTIMER <键> <秒> <类型>` | 设置玩家**定时器** |

```
@INFO 张三
@TIME
@TRIGGER 攻城开始
@SETQUEST 1001 1
```

> `@SETQUEST` 的状态码：`0` = 清除任务（同时从已完成列表移除），`1` = 标记为已完成，其他值只是把任务从进行中列表移除。

> `@DECO` 放下的装饰物**需要重启才会消失**，别乱放。

---

### 5.10 玩家管理（1 条）

| 命令 | 语法 | 说明 |
|---|---|---|
| `@KILL` | `@KILL` / `@KILL <玩家名>` | **强制杀死**自己或指定玩家 |

```
@KILL 张三
```

---

### 5.11 GM 命令的快捷参考（按首字母）

| 字母 | 命令 |
|---|---|
| A | `@ADDINVENTORY` `@ADDSTORAGE` `@ADJUSTPKPOINT` `@ALLOWOBSERVE` `@ARCHIVEPLAYER` `@AWAKENING` |
| B | `@BACKUPPLAYER` |
| C | `@CHANGECLASS` `@CHANGEFLAG` `@CHANGEFLAGCOLOUR` `@CHANGEGENDER` `@CLEARBAG` `@CLEARBUFFS` `@CLEARFLAGS` `@CLEARIPBLOCKS` `@CLEARMOB` `@CLEARQUESTS` `@CREATEGUILD` |
| D | `@DECO` `@DELETESKILL` `@DIE` |
| G | `@GAMEMASTER` `@GATES` `@GIVECREDIT` `@GIVEGOLD` `@GIVEPEARLS` `@GIVESKILL` `@GOTO` |
| H | `@HAIR` |
| I | `@INFO` |
| K | `@KILL` |
| L | `@LEVEL` `@LEVELHERO` `@LISTFLAGS` `@LOADPLAYER` `@LOGIN` |
| M | `@MAKE` `@MAP` `@MAPMOVE` `@MOB` |
| O | `@OBSERVE` `@OBSERVER` |
| R | `@RECALL` `@RECALLMOB` `@RELOADDROPS` `@RELOADNPCS` `@REMOVEAWAKENING` `@RESETCONQUEST` `@RESTOREPLAYER` `@REVIVE` `@RIDE` `@ROLL` |
| S | `@SETFLAG` `@SETLIGHT` `@SETQUEST` `@SETTIMER` `@STARTCONQUEST` `@STARTWAR` `@SUMMONHERO` `@SUPERMAN` |
| T | `@TIME` `@TOGGLETRANSFORM` `@TRIGGER` |
| 中文 | `@传送` `@探测` `@加入行会` `@退出行会` `@心心相映` `@天人合一` `@经天纬地` `@允许交易` |

---

## 六、触发钩子对照表

系统脚本在**特定游戏事件**发生时自动执行。所有钩子都定义在 `Envir\SystemScripts\00Default\` 下的对应文件里。

### 6.1 事件对照表

| 事件 | 标签写法 | 定义文件 | 触发时机 |
|---|---|---|---|
| 登录 | `[@_LOGIN]` | `Login.txt` | 玩家进入游戏 |
| 升级 | `[@_LEVELUP]` | `LevelUp.txt` | 玩家升级的**瞬间** |
| 使用物品 | `[@_USEITEM(Shape值)]` | `UseItems.txt` | 玩家双击使用某物品 |
| 走到坐标 | `[@_MAPCOORD(地图,X,Y)]` | `MapCoords.txt` | 玩家走到指定坐标 |
| 进入地图 | `[@_MAPENTER(地图名)]` | `MapEnter.txt` | 玩家进入某地图 |
| 死亡 | `[@_DIE]` | `Die.txt` | 玩家死亡 |
| 接受任务 | `[@_ONACCEPTQUEST(任务索引)]` | `OnAcceptQuests.txt` | 玩家接下任务 |
| 完成任务 | `[@_ONFINISHQUEST(任务索引)]` | `OnFinishQuests.txt` | 玩家完成任务 |
| 每日首次 | `[@_DAILY]` | `Daily.txt` | 玩家每天第一次进游戏 |
| 手动触发 | `[@_TRIGGER(参数)]` | `Trigger.txt` | GM 用 `@TRIGGER` 或脚本调用 |
| 自定义命令 | `[@_CUSTOMCOMMAND(命令名)]` | `CustomCommands.txt` | 玩家在聊天框输入 `@命令名` |
| 客户端 | `[@_CLIENT]` | `Client.txt` | 客户端请求（如打开网页） |

**各文件的统一结构：** 定义好标签页 → 用 `#INCLUDE` 把具体逻辑引到子目录的文件里。

```
;; 例：MapEnter.txt
[@_MAPENTER(0)]
#INCLUDE [SystemScripts\00Default\地图进入触发\0.txt] @Main
```

### 6.2 逐条说明与实例

#### 登录触发 `[@_LOGIN]`

```
[@_LOGIN]
#IF
ISADMIN
#INCLUDE [SystemScripts\00Default\登录触发\[952].txt] @Main
```

> 注意：这个页面在**每次登录**都会执行，别在这里写 `GIVEITEM`（否则每次上线都发）。

#### 升级触发 `[@_LEVELUP]`

```
[@_LEVELUP]
#IF
LEVEL == 7
#ACT
GIVESKILL Fencing 0
GIVEITEM 木剑 1
```

> **只对"升级的那一瞬间"生效**。已有角色不会补发。想让老角色也能补领，得另做一个查询当前等级的 NPC。

#### 物品触发 `[@_USEITEM(Shape值)]`

参数是物品的 **`Shape` 字段值**，不是物品名字。查看物品 Shape 要去数据库/导出表里查。

```
[@_UseItem(11)]
#ACT
REDUCEPKPOINT 10
LocalMessage "PK值降低了10点" Hint
```

#### 地图坐标触发 `[@_MAPCOORD(地图,X,Y)]`

```
[@_MAPCOORD(3,861,686)]
#INCLUDE [SystemScripts\00Default\地图坐标触发\PenalCavern.txt] @Main
```

> 坐标必须**精确匹配**。想覆盖一片区域就多写几行。

#### 自定义命令 `[@_CUSTOMCOMMAND(命令名)]`

这是**给玩家用的自定义指令**，玩家在聊天框输入 `@命令名` 就触发。

```
[@_CUSTOMCOMMAND(召唤神兽)]
#INCLUDE [SystemScripts\00Default\命令物品\神兽护卫.txt] @Main
```

玩家输入 `@召唤神兽` 即可。

**规则：**
- 命令名**不能和 GM 命令重名**（GM 命令优先匹配）
- 参数里的命令名**大小写不敏感**
- 页里的 `#SAY` 内容会正常弹给玩家

#### 每日首次触发 `[@_DAILY]`

玩家**每天第一次**进游戏时执行一次。适合做每日签到、每日礼包。

#### 手动触发 `[@_TRIGGER(参数)]`

GM 用 `@TRIGGER <参数>` 就能触发，也可以从别的脚本里调。

```
[@_TRIGGER(攻城开始)]
#ACT
GLOBALMESSAGE "攻城战即将开始，请做好战斗准备！" Announcement
```

GM 输入：`@TRIGGER 攻城开始`

### 6.3 ⚠️ 两个必须知道的坑

#### 坑 1：`MapLeave.txt` 是**失效配置**

你服务端的 `00Default.txt` 里有：

```
#INSERT [SystemScripts\00Default\MapLeave.txt] @Main
```

`MapLeave.txt` 里也写了一堆 `[@_MAPLEAVE(地图名)]` 页面。

**但是本服务端源码里根本没有"离开地图"这个事件** —— `DefaultNPCType` 枚举只有 12 个值（Login / LevelUp / UseItem / MapCoord / MapEnter / Die / Trigger / CustomCommand / OnAcceptQuest / OnFinishQuest / Daily / Client），**没有 MapLeave**，服务端代码里也搜不到任何 `MapLeave` 的调用。

**结论**：`MapLeave.txt` 里的所有页面**永远不会被触发**。别在那里写逻辑，写了也不执行。要做"离开地图"的效果，用别的方式（例如在目标地图的 `[@_MAPENTER]` 里做补偿逻辑）。

#### 坑 2：`OnFinishQuests.txt` 里的 `PARAM1` 是**老引擎写法**

现有文件里写的是：

```
[@_ONFINISHQUEST(PARAM1)]
#ACT
SET [542] 1
```

但本服务端传进来的参数是**真实的数字任务索引**（`OnFinishQuest(149)`），标签需要写成 `[@_ONFINISHQUEST(149)]`。`PARAM1` 这种占位符是 Delphi 版的遗留写法，在这里**匹配不上**。

**要启用任务完成触发，请改成数字索引写法。** 任务索引可以在客户端的任务配置里查到。

---

## 七、完整示例

### 示例 1：新手礼包 NPC（含防重复领取）

```
[@Main]
#SAY
欢迎来到本服，<$USERNAME>！\
你当前 <$LEVEL> 级 <$CLASS>。\
<领取新手礼包/@领礼包>  <离开/@exit>

[@领礼包]
#IF
CHECK [100] 1
#SAY
你已经领过新手礼包了。\
<返回/@Main>
#ACT
BREAK

#IF
HASBAGSPACE < 12
#SAY
你的背包空格不足 12 个，请先清理背包。\
<返回/@Main>
#ACT
BREAK

#ACT
SET [100] 1
GIVEITEM 金创药(小) 50
GIVEITEM 回城卷 10
GIVEGOLD 50000
LOCALMESSAGE "新手礼包已发放，祝你游戏愉快！" Hint
#SAY
礼包发放成功！\
<返回/@Main>
```

**要点**：用标志位 `[100]` 记录已领取状态（跨下线保留），用 `HASBAGSPACE` 防止背包满物品丢失，用 `BREAK` 让多段互斥。

---

### 示例 2：等级奖励（分档 + 分职业）

```
[@领奖励]
#IF
LEVEL < 10
#SAY
等级不足 10 级。\
<返回/@Main>
#ACT
BREAK

#IF
CHECKCLASS 战士
LEVEL >= 10
LEVEL < 20
#ACT
GIVEITEM 木剑 1
GIVEITEM 布衣(男) 1
LOCALMESSAGE "恭喜升到 10 级，奖励已发放！" Hint
```

> 完整的分档脚本可以直接参考我为你生成的 `Envir\SystemScripts\00Default\等级触发\升级装备奖励.txt`（挂载在 `LevelUp.txt` 的 `[@_LevelUp]` 页上）。

---

### 示例 3：泡点升级（循环定时器）

```
[@Main]
#SAY
泡点说明：每 1 秒获得 10000 经验，下线自动停止。\
<开始泡点/@开始泡点>  <停止泡点/@停止泡点>  <查询状态/@状态>  <离开/@exit>

[@开始泡点]
#IF
CHECKLOOPTIMER paodian
#SAY
泡点已经在运行中。\
<返回/@Main>
#ELSEACT
SETLOOPTIMER paodian 1 paodian_reward
#ELSESAY
泡点已开启，开始挂机吧！\
<返回/@Main>

[@停止泡点]
#ACT
EXPIRETIMER paodian
LOCALMESSAGE "泡点已停止" Hint
#SAY
泡点已停止。\
<返回/@Main>

[@状态]
#IF
CHECKLOOPTIMER paodian
#SAY
泡点正在运行中。\
<返回/@Main>
#ELSESAY
泡点没有开启。\
<返回/@Main>

[@paodian_reward]
#ACT
GIVEEXP 10000
LOCALMESSAGE "泡点奖励：获得 10000 点经验" Hint
```

**要点**：
- `[@paodian_reward]` 里**不能写 `#SAY`**（静默执行，写了也没用）
- 目标页不需要按钮指向，引擎已支持动态页解析
- 玩家下线后定时器自动清除

---

### 示例 4：自定义玩家命令（不消耗GM权限）

在 `Envir\SystemScripts\00Default\CustomCommands.txt` 里加：

```
;; 玩家查询邮件
[@_CUSTOMCOMMAND(我的邮件)]
#SAY
你有 <$PARCELAMOUNT> 封未领取的邮件。\
请到各大主城邮筒处领取。

;; 玩家查询泡点
[@_CUSTOMCOMMAND(泡点状态)]
#IF
CHECKLOOPTIMER paodian
#SAY
泡点运行中，每秒 10000 经验。
#ELSESAY
泡点未开启，去边界村找 NPC 开启。
```

玩家在聊天框输入 `@我的邮件` 或 `@泡点状态` 即可。

> 加完后需要在游戏里用 `@RELOADNPCS` 重载，或重启服务端。

---

### 示例 5：按星期开关的双倍经验活动

```
[@_MAPENTER(0)]
#IF
DAYOFWEEK SUNDAY
#ACT
GIVEBUFF 30%经验值 3600 true false false
LOCALMESSAGE "今天是周日，双倍经验活动已自动开启！" Announcement
```

> Buff 名要存在于 `Envir\SetBuffs.txt`。

---

### 示例 6：邮件发奖（不受背包限制）

```
[@发奖]
#ACT
COMPOSEMAIL "活动奖励" System
ADDMAILGOLD 500000
ADDMAILITEM 金创药(小) 100
ADDMAILITEM 回城卷 50
SENDMAIL
LOCALMESSAGE "奖励已发送至你的邮箱，请查收" Hint
#SAY
奖励已发放，请到邮箱领取。\
<返回/@Main>
```

---

## 八、常见坑速查

| 现象 | 原因 | 解决 |
|---|---|---|
| 脚本改了没变化 | 服务端不会自动扫描文件 | 输入 `@RELOADNPCS` 或重启 |
| 新写的 NPC 游戏里看不到 | 只放了 `.txt`，数据库里没挂 NPC | 在 DB 里加 `NPCInfo`，指向脚本文件 |
| `#SAY` 里写了中文但显示乱码 | 文件存成了 GBK | 用 UTF-8 重新保存 |
| 多个 `#IF` 段的结果都显示了 | 段之间是"各自求值"不是 if-else | 在每段 `#ACT` 末尾加 `BREAK` |
| `LOCALMESSAGE` 不生效 | 少了聊天类型参数 | `LOCALMESSAGE "文本" Hint`（必须带类型） |
| 物品发出去了但玩家没收到 | 背包满时物品被直接丢弃 | 先 `HASBAGSPACE` 判断，或用邮件发 |
| `GIVESKILL 火球术` 无效 | 技能名必须用英文枚举 | `GIVESKILL FireBall 0` |
| `LEVEL 30 >` 判断不对 | 操作符和数值写反了 | `LEVEL > 30` |
| 定时器到点了什么都不发生 | 目标页没有任何按钮指向，或写了 `#SAY` | 本服已修复动态页解析；确保目标页只写 `#ACT` |
| `CHECKITem` 判断总是失败 | 物品名和数据库不一致（含空格/括号） | 用双引号包住物品名，去数据库核对全名 |
| `@TRIGGER` 无效 | 参数和脚本标签里的参数不一致 | GM 输入要与 `[@_TRIGGER(xxx)]` 的 `xxx` 完全一致 |
| `@CUSTOMCOMMAND` 不触发 | 命令名和某个 GM 命令重名 | 换个名字 |
| 地图离开触发没反应 | 本服务端无此事件 | 见 6.3 坑 1 |

---

## 附：数据核对方法

本手册中的所有命令都是**从代码里逐条提取**的。如果你后续加了新命令或者要核实，用下面的方法自查：

### 一键重新提取（推荐）

工程里已放好提取脚本：`mir2-20241027\Tools\extract_commands.py`

```bash
cd D:\BaiduNetdiskDownload\mir2-20241027\Tools
python extract_commands.py            # 生成清单到 out\ 目录
python extract_commands.py --list     # 只打印命令名列表
```

运行后应输出：

```
检查命令（#IF 下用） :  46 条
动作命令（#ACT 下用）:  92 条
脚本变量 <$XXX>      :  47 个
GM 命令 @XXX         :  71 条
```

如果数字变了，说明命令集有增减，可以在 `out\04_GM命令.txt` 里对照差异。

### 手工核对

**1. 查所有脚本命令名**

在 `Server\MirObjects\NPC\NPCSegment.cs` 里：
- `ParseCheck` 方法内的 `switch` → **检查命令**（约 114-400 行）
- `ParseAct` 方法内的第一个 `switch` → **动作命令**（约 416-1180 行）
- `ParseAct` 方法内的 `switch (innerMatch)` → **脚本变量**（约 1205 行起）

**2. 查 GM 命令**

在 `Server\MirObjects\PlayerObject.cs` 里，搜索 `else if (message.StartsWith("@"))`，其后紧跟的 `switch` 就是 GM 命令全集（约 2086-4073 行）。

**3. 查技能枚举名**

用本服导出的技能表：`Tools\magic_dump.csv`（含中文名 ↔ `Spell` 枚举名 ↔ 学习等级）。

**4. 查物品准确名字**

用本服导出的物品表：`Tools\item_dump.csv`（4238 件物品的完整清单）。

---

*本手册基于服务端源码逐条提取核对生成，覆盖 46 条检查命令 + 92 条动作命令 + 47 个变量 + 71 条 GM 命令。*

