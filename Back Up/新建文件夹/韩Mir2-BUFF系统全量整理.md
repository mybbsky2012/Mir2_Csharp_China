# 韩Mir2 BUFF 系统全量整理

> 整理日期：2026-10-21 ｜ 数据来源：源码 `D:\mir2-20241027`（Shared/Enums.cs、Server/MirDatabase/BuffInfo.cs、BuffConfig.cs）+ 线上配置 `Mir2Server\Configs\BuffSystem.ini`、`Envir\SetBuffs.txt`

---

## 一、系统架构总览

BUFF 系统由四层组成，自上而下：

| 层 | 位置 | 作用 |
|---|---|---|
| ① 枚举定义 | `Shared\Enums.cs` → `BuffType`（byte，0~253） | 每个 BUFF 的唯一 ID |
| ② 注册表 | `Server\MirDatabase\BuffInfo.cs` → `BuffInfo.Load()` | 声明每个 BUFF 的可见性 / 堆叠方式 / 属性标记；**未注册的枚举值一用就抛 NotImplementedException** |
| ③ 自定义配置 | `Server\MirDatabase\BuffConfig.cs` + `Configs\BuffSystem.ini` | 24 个自定义槽位（Custom1~24）+ 可覆盖已有 BUFF 的名字/图标/属性 |
| ④ 运行实例 | `Server\MirDatabase\BuffInfo.cs` → `Buff` 类 | 挂在角色身上的实例：Stats 数值、ExpireTime 到期时间、Caster 施法者 |

客户端下发：`Buff.ToClientBuff()` 生成 `ClientBuff`（Shared\Data\ClientData.cs），**显示名 Name 与图标 Icon 随封包下发**——自定义 BUFF 不需要客户端改枚举。

### 堆叠方式（BuffStackType）

| 值 | 名称 | 含义 |
|---|---|---|
| 0 | None | 不堆叠，已存在则忽略 |
| 1 | ResetDuration | 重复获得 → 重置时长（最常用） |
| 2 | StackDuration | 重复获得 → 时长累加 |
| 3 | StackStat | 数值叠加 |
| 4 | StackStatAndDuration | 数值+时长都叠加 |
| 5 | Infinite | 永久（无到期时间，如戒指/项链/管理） |
| 6 | ResetStat | 重置数值，保留时长 |
| 7 | ResetStatAndDuration | 重置数值+时长（属性药水类默认） |
| 8 | AttrStackStat | 按属性叠加（龙之特效类） |
| 9 | AttrStackStatAndDuration | 按属性叠加+时长 |

### 属性标记（BuffProperty，可按位或叠加）

| 值 | 名称 | 含义 |
|---|---|---|
| 1 | RemoveOnDeath | 死亡时移除 |
| 2 | RemoveOnExit | 退出游戏时移除 |
| 4 | Debuff | 负面效果 |
| 8 | PauseInSafeZone | 安全区内暂停计时 |

---

## 二、BuffType 枚举全表（131 项）

### 2.1 技能类（Magic，1~34）

| ID | 名称 | ID | 名称 |
|---|---|---|---|
| 1 | 时间之殇 | 18 | 天务 |
| 2 | 隐身术 | 19 | 精神状态 |
| 3 | 体迅风 | 20 | 先天气功 |
| 4 | 轻身步 | 21 | 深延术 |
| 5 | 血龙剑法 | 22 | 血龙水 |
| 6 | 幽灵盾 | 23 | 金刚不坏 |
| 7 | 神圣战甲术 | 24 | 金刚不坏秘籍 |
| 8 | 风身术 | 25 | 魔法盾 |
| 9 | 无极真气 | 26 | 金刚术 |
| 10 | 护身气幕 | 27 | 天上秘术 |
| 11 | 剑气爆 | 28 | 万效符 |
| 12 | 诅咒术 | 29 | 万效符秘笈 |
| 13 | 月影术 | 30 | 影化连击 |
| 14 | 烈火身 | 31 | 灭杀窗口 |
| 15 | 气流术 | 32 | 月华乱舞减准 |
| 16 | 吸血地闪 | 33 | 万效灵魂火符 |
| 17 | 毒魔闪 | 34 | 万效秘灵魂火符 |

### 2.2 怪物类（Monster，50~70）

| ID | 名称 | ID | 名称 |
|---|---|---|---|
| 50 | HornedArcherBuff | 61 | ReaperPriestBuff |
| 51 | ColdArcherBuff | 62 | 至尊威严 |
| 52 | HornedColdArcherBuff | 63 | 伤口加深 |
| 53 | GeneralMeowMeowShield | 64 | 死亡印记 |
| 54 | 惩戒真言 | 65 | RiklebitesShield |
| 55 | 御体之力 | 66 | 麻痹状态 |
| 56 | HornedWarriorShield | 67 | 绝对封锁 |
| 57 | HornedCommanderShield | 68 | Mon564NSealing |
| 58 | 失明状态 | 69 | 烈火焚烧 |
| 59 | ChieftainSwordBuff | 70 | 防御诅咒 |
| 60 | 寒冰护甲 | | |

### 2.3 特殊类（Special，100~122）

| ID | 名称 | 说明 |
|---|---|---|
| 100 | 游戏管理 | GM 隐身/管理状态 |
| 101 | General | 通用（历史遗留） |
| 102 | 获取经验提升 | 经验药水（注册在 Stats 组） |
| 103 | 物品掉落提升 | 掉落药水（注册在 Stats 组） |
| 104 | 金币辉煌 | 金币加成 |
| 105 | 背包负重提升 | 负重药水（注册在 Stats 组） |
| 106 | 变形效果 | 变身 |
| 107 | 心心相映 | 组队特效 |
| 108 | 衣钵相传 | 师徒 |
| 109 | 火传穷薪 | 师徒 |
| 110 | 公会特效 | 公会 |
| 111 | Prison | 监禁 |
| 112 | 精力充沛 | 精力 |
| 113 | 技巧项链 | 装备 |
| 114 | 隐身戒指 | 装备 |
| 115 | 新人特效 | 新手保护 |
| 116 | 技能经验提升 | 技能经验药水（注册在 Stats 组） |
| 117 | 英雄灵气 | 英雄系统 |
| 118 | 暗影侵袭 | 负面（SetBuffs 属性全为负值） |
| 119 | 攻击型绝技 | 绝技系统 |
| 120 | 防御型绝技 | 绝技系统 |
| 121 | 技能型绝技 | 绝技系统 |
| 122 | 共用型绝技 | 绝技系统 |

### 2.4 属性类（Stats，200~228）

| ID | 名称 | ID | 名称 |
|---|---|---|---|
| 200 | 攻击力提升 | 215 | 华丽雨光 |
| 201 | 魔法力提升 | 216 | 龙之特效 |
| 202 | 道术力提升 | 217 | 龙的特效 |
| 203 | 攻击速度提升 | 218 | 增强组队 |
| 204 | 生命值提升 | 219 | 天灵水 |
| 205 | 法力值提升 | 220 | 玉清水 |
| 206 | 防御提升 | 221 | 甜筒HP |
| 207 | 魔法防御提升 | 222 | 甜筒MP |
| 208 | 奇异药水 | 223 | 内尔族的灵药 |
| 209 | 包容万斤 | 224 | 摩鲁的赤色药剂 |
| 210 | 龍之祝福 | 225 | 摩鲁的青色药剂 |
| 211 | 准确命中提升 | 226 | 摩鲁的黄色药剂 |
| 212 | 敏捷躲避提升 | 227 | 古代宗师祝福 |
| 213 | 安息之气 | 228 | 黄金宗师祝福 |
| 214 | 远古气息 | | |

### 2.5 自定义槽位（Custom，230~253）

| 槽位 | 枚举 | 槽位 | 枚举 |
|---|---|---|---|
| 1 | Custom1 = 230 | 13 | Custom13 = 242 |
| 2 | Custom2 = 231 | 14 | Custom14 = 243 |
| 3 | Custom3 = 232 | 15 | Custom15 = 244 |
| 4 | Custom4 = 233 | 16 | Custom16 = 245 |
| 5 | Custom5 = 234 | 17 | Custom17 = 246 |
| 6 | Custom6 = 235 | 18 | Custom18 = 247 |
| 7 | Custom7 = 236 | 19 | Custom19 = 248 |
| 8 | Custom8 = 237 | 20 | Custom20 = 249 |
| 9 | Custom9 = 238 | 21 | Custom21 = 250 |
| 10 | Custom10 = 239 | 22 | Custom22 = 251 |
| 11 | Custom11 = 240 | 23 | Custom23 = 252 |
| 12 | Custom12 = 241 | 24 | Custom24 = 253 |

> byte 上限 255，故自定义槽位只到 253。显示名与图标由服务端随封包下发，**不需要为槽位起枚举中文名**。

---

## 三、BuffInfo 注册表（107 项，含堆叠/标记/可见性）

> Visible=✔ 表示对其他玩家可见（默认不可见）。

### 3.1 技能类

| BUFF | 堆叠 | 标记 | 可见 |
|---|---|---|---|
| 时间之殇 | ResetDuration | — | |
| 隐身术 | ResetDuration | — | |
| 体迅风 | ResetDuration | — | |
| 轻身步 | ResetDuration | — | ✔ |
| 血龙剑法 | ResetDuration | — | ✔ |
| 幽灵盾 | ResetDuration | — | |
| 神圣战甲术 | ResetDuration | — | |
| 风身术 | ResetDuration | — | |
| 无极真气 | ResetStatAndDuration | — | |
| 护身气幕 | ResetDuration | — | |
| 剑气爆 | ResetDuration | — | |
| 诅咒术 | ResetDuration | RemoveOnDeath+Debuff | |
| 月影术 | ResetDuration | — | ✔ |
| 烈火身 | ResetDuration | — | ✔ |
| 气流术 | ResetDuration | — | |
| 吸血地闪 | ResetDuration | — | ✔ |
| 毒魔闪 | ResetDuration | — | ✔ |
| 天务 | ResetDuration | — | ✔ |
| 精神状态 | Infinite | — | |
| 先天气功 | ResetDuration | — | ✔ |
| 深延术 | ResetDuration | — | ✔ |
| 血龙水 | ResetDuration | — | ✔ |
| 金刚不坏 | ResetDuration | — | ✔ |
| 金刚不坏秘籍 | ResetDuration | — | ✔ |
| 天上秘术 | ResetDuration | — | ✔ |
| 魔法盾 | ResetDuration | — | |
| 金刚术 | ResetDuration | — | |
| 万效符 | ResetDuration | — | ✔ |
| 万效符秘笈 | ResetDuration | — | ✔ |
| 影化连击 | ResetDuration | — | ✔ |
| 灭杀窗口 | ResetDuration | — | ✔ |
| 月华乱舞减准 | ResetDuration | Debuff+RemoveOnDeath | ✔ |
| 万效灵魂火符 | ResetDuration | — | ✔ |
| 万效秘灵魂火符 | ResetDuration | — | ✔ |

### 3.2 怪物类

| BUFF | 堆叠 | 标记 | 可见 |
|---|---|---|---|
| HornedArcherBuff | ResetDuration | — | ✔ |
| ColdArcherBuff | ResetDuration | — | ✔ |
| HornedColdArcherBuff | ResetDuration | — | ✔ |
| GeneralMeowMeowShield | ResetDuration | — | ✔ |
| 惩戒真言 | ResetDuration | Debuff | |
| 御体之力 | ResetDuration | — | ✔ |
| HornedWarriorShield | ResetDuration | — | ✔ |
| HornedCommanderShield | ResetDuration | — | ✔ |
| 失明状态 | ResetDuration | RemoveOnDeath+Debuff | |
| ChieftainSwordBuff | ResetDuration | — | ✔ |
| 寒冰护甲 | ResetDuration | — | ✔ |
| ReaperPriestBuff | ResetDuration | — | ✔ |
| 至尊威严 | ResetDuration | — | ✔ |
| 伤口加深 | ResetDuration | — | ✔ |
| 死亡印记 | ResetDuration | — | ✔ |
| RiklebitesShield | ResetDuration | — | ✔ |
| 麻痹状态 | ResetDuration | Debuff | ✔ |
| 绝对封锁 | ResetDuration | Debuff | ✔ |
| Mon564NSealing | ResetDuration | — | ✔ |
| 烈火焚烧 | ResetDuration | Debuff | ✔ |
| 防御诅咒 | ResetDuration | Debuff | ✔ |

### 3.3 特殊类

| BUFF | 堆叠 | 标记 | 可见 | 备注 |
|---|---|---|---|---|
| 游戏管理 | Infinite | — | 随 GM 特效开关 | Settings.GameMasterEffect |
| 衣钵相传 | Infinite | — | | 师徒 |
| 火传穷薪 | Infinite | — | | 师徒 |
| 公会特效 | Infinite | — | | 公会 |
| 技巧项链 | Infinite | — | | 装备常驻 |
| 隐身戒指 | Infinite | — | | 装备常驻 |
| 变形效果 | None | — | | 变身 |
| 心心相映 | Infinite | RemoveOnExit | | 组队 |
| 精力充沛 | ResetDuration | — | | |
| Prison | None | — | | 监禁 |
| General | None | — | | 遗留 |
| 新人特效 | Infinite | RemoveOnExit | | 新手 |
| 英雄灵气 | Infinite | — | | 英雄 |
| 暗影侵袭 | Infinite | RemoveOnExit | | 负面 |
| 攻击型绝技 | Infinite | — | | 绝技 |
| 防御型绝技 | Infinite | — | | 绝技 |
| 技能型绝技 | Infinite | — | | 绝技 |
| 共用型绝技 | Infinite | — | | 绝技 |

### 3.4 属性类

| BUFF | 堆叠 | 标记 | 可见 |
|---|---|---|---|
| 获取经验提升 | ResetStatAndDuration | PauseInSafeZone | |
| 物品掉落提升 | ResetStatAndDuration | PauseInSafeZone | |
| 金币辉煌 | StackDuration | PauseInSafeZone | |
| 背包负重提升 | ResetStatAndDuration | — | |
| 技能经验提升 | ResetStatAndDuration | PauseInSafeZone | |
| 攻击力提升 | ResetStatAndDuration | PauseInSafeZone | |
| 魔法力提升 | ResetStatAndDuration | PauseInSafeZone | |
| 道术力提升 | ResetStatAndDuration | PauseInSafeZone | |
| 攻击速度提升 | ResetStatAndDuration | PauseInSafeZone | |
| 生命值提升 | ResetStatAndDuration | — | |
| 法力值提升 | ResetStatAndDuration | — | |
| 防御提升 | ResetStatAndDuration | PauseInSafeZone | |
| 魔法防御提升 | ResetStatAndDuration | PauseInSafeZone | |
| 奇异药水 | StackDuration | PauseInSafeZone | |
| 包容万斤 | StackDuration | PauseInSafeZone | |
| 龍之祝福 | StackDuration | PauseInSafeZone | |
| 准确命中提升 | ResetStatAndDuration | PauseInSafeZone | |
| 敏捷躲避提升 | ResetStatAndDuration | PauseInSafeZone | |
| 安息之气 | None | RemoveOnDeath | |
| 远古气息 | None | RemoveOnDeath | |
| 华丽雨光 | None | RemoveOnDeath | ✔ |
| 龙之特效 | AttrStackStat | RemoveOnDeath | ✔ |
| 龙的特效 | AttrStackStat | RemoveOnDeath | ✔ |
| 增强组队 | AttrStackStat | RemoveOnDeath | ✔ |
| 内尔族的灵药 | None | RemoveOnDeath | |
| 天灵水 | None | RemoveOnDeath | |
| 玉清水 | None | RemoveOnDeath | |
| 甜筒HP | None | RemoveOnDeath | |
| 甜筒MP | None | RemoveOnDeath | |
| 摩鲁的赤色药剂 | None | RemoveOnDeath | |
| 摩鲁的青色药剂 | None | RemoveOnDeath | |
| 摩鲁的黄色药剂 | None | RemoveOnDeath | |
| 古代宗师祝福 | None | RemoveOnDeath | |
| 黄金宗师祝福 | None | — | |

> 属性类数值（加多少攻/防/经验）不在 BuffInfo 里，而是来自 **SetBuffs.txt**（见第五章）或使用时的 Stats 参数（药水/NPC/GM 入口）。

---

## 四、自定义 BUFF 机制（BuffSystem.ini）

配置文件：`Mir2Server\Configs\BuffSystem.ini`（UTF-8 无 BOM，`;` 注释），由 GM 工具「数据库 → 自定义BUFF属性」维护，改完 `@RELOADBUFF` 即时生效（无需重启）。

### 4.1 节的两种写法

| 写法 | 含义 |
|---|---|
| `[1]`~`[24]` | 自定义槽位 → BuffType.Custom1~Custom24 |
| `[隐身术]` 等枚举名 | **覆盖**已有 BUFF 的名字/图标/堆叠/属性（只覆盖写了的字段） |

### 4.2 字段说明

| 字段 | 说明 |
|---|---|
| Name | 显示名（客户端 BUFF 提示框标题；自定义槽位必填） |
| Icon | 图标索引，见 4.3 |
| Visible | 是否对其他玩家可见（默认 True） |
| Stack | 堆叠方式（10 种，见第一章表） |
| Property | 标记，可 `|` 叠加：RemoveOnDeath/RemoveOnExit/Debuff/PauseInSafeZone |
| Desc | 说明文本（仅 GM 工具内显示） |
| Stat.属性=数值 | 属性加成，写「基础」或「基础+每级」，如 `Stat.MaxAC=5+2`（施法者每级再+2，无施法者按目标等级） |

### 4.3 图标编码（ClientBuff.Icon）

| 值 | 图库 |
|---|---|
| 0 | 客户端原有枚举名硬编码映射（老 BUFF 走这条） |
| 10000+ | Prguse2 图库 |
| 20000+ | MagIcon 图库（BUFF 栏 24×22） |
| 40000+ | MagIcon2 图库（值为 40000 + 技能Icon×2，技能栏 36×34） |

### 4.4 当前线上配置

| 节 | 名称 | 属性 | 说明 |
|---|---|---|---|
| [1] | 攻速水 | Stat.攻击速度=8 | 攻速+8 |
| [2] | 攻击水 | Stat.MaxDC=5 | 攻击上限+5 |

> 脚本/GM 引用自定义 BUFF 名时，三种写法都认：**枚举名（Custom1）/ ini 显示名（攻速水）/ 槽位号（1）**（`BuffConfig.ResolveScriptName`）。

---

## 五、SetBuffs.txt 属性表（线上 22 条）

文件：`Mir2Server\Envir\SetBuffs.txt`。格式：`名称;属性=值;属性=值;;`（半角分号分隔）。

| 名称 | 属性 | GIVEBUFF 可达 |
|---|---|---|
| 破天的核心 | 生命值强化=50；法力值强化=50；MaxDC=100；幸运=1 | ✘（非枚举名，遗留） |
| 摩鲁的赤色药剂 | MaxDC=50；MaxMC=50；MaxSC=50 | ✔ |
| 摩鲁的青色药剂 | MaxAC=30；MaxMAC=30；幸运=1 | ✔ |
| 摩鲁的黄色药剂 | 生命值强化=30；法力值强化=30；幸运=1 | ✔ |
| 暗影侵袭 | MaxDC=-50；MaxMC=-50；MaxSC=-50；生命值强化=-30；法力值强化=-30 | ✔ |
| 古代宗师祝福 | 神圣=5 | ✔ |
| 黄金宗师祝福 | 神圣=7 | ✔ |
| 华丽雨光 | MaxDC=3；MaxMC=3；MaxSC=3；敏捷=3；中毒恢复=1；经验收益=30 | ✔ |
| 丝绸官服 | MaxDC=7；MaxMC=7；MaxSC=7；MaxAC=8；MaxMAC=8；掉落收益=30 | ✘（遗留） |
| 安息之气 | 经验收益=30 | ✔ |
| 30%经验值 | 经验收益=30 | ✘（遗留） |
| 远古气息 | 经验收益=50 | ✔ |
| 50%经验值 | 经验收益=50 | ✘（遗留） |
| 落物纷飞 | 掉落收益=100 | ✘（遗留） |
| 内尔族的灵药 | HP=500；MP=200 | ✔ |
| 天灵水 | MinDC=3；MaxDC=3；敏捷=3；准确=3；幸运=3；HP=70 | ✔ |
| 玉清水 | 双防/双攻/幸运各 1~2（全属性小加成） | ✔ |
| 天人水 | MaxMAC=2；幸运=1；HP=130；MP=130；掉落收益=30 | ✘（遗留） |
| 知人水 | MaxDC=1；MaxMC=1；MaxSC=1；MaxAC=2；经验收益=30 | ✘（遗留） |
| 龙之特效 | MaxDC=0；MaxMC=0；MaxSC=0；MaxAC=0；MaxMAC=0；HP=0；MP=0（占位，实际值代码内按来源计算） | ✔ |
| 龙的特效 | 同上 | ✔ |
| 监禁 | MaxAC=-255；MaxMAC=-255 | ✘（枚举是 Prison） |

---

## 六、BUFF 的四类获得入口

### 6.1 物品使用（PlayerObject.UseItem，药水类）

| 药水 Shape | 效果 | 时长 | 属性来源 |
|---|---|---|---|
| **3** | 属性药水（可同时挂多个） | **物品 Durability = 分钟数**（1 小时 = 60） | 物品属性页：MaxDC→攻击力提升、MaxMC→魔法力提升、MaxSC→道术力提升、攻击速度→攻击速度提升、HP→生命值提升、MP→法力值提升、MaxAC→防御提升、MaxMAC→魔法防御提升、背包负重→背包负重提升、准确→准确命中提升、敏捷→敏捷躲避提升（>0 才挂） |
| 4 | 经验药水 → 获取经验提升 | Durability 分钟 | 数值取物品「幸运」属性 → Stat.经验增长数率 |
| 5 | 掉落药水 → 物品掉落提升 | Durability 分钟 | 数值取物品「幸运」属性 → Stat.物品掉落数率 |
| 8 | 技能经验药水 → 技能经验提升 | Durability 分钟 | 固定 3 倍熟练度 |
| 2 | 神秘水（解诅咒，非 BUFF） | — | — |

> ★**不动代码做限时属性的标准做法**：药水 Shape=3 + Durability=时长分钟数 + 属性页填数值。

### 6.2 NPC 脚本（NPCSegment.cs）

```
GIVEBUFF <BUFF名> <秒数> <infinite> <visible> <stackable>
REMOVEBUFF <BUFF名>
```

- BUFF 名：枚举名 / BuffSystem.ini 显示名 / 自定义槽位号 三种写法都认；解析不到**静默失败**
- 属性数值来自 SetBuffs.txt 里以该 BUFF 名开头的那一行
- ★**冲突规则**：命令第 6 个参数起可追加 `属性=值`，但只允许补 SetBuffs 行里**没有**的属性；与行内已有属性重复或行不存在（无零值行）时 `canAddBuff=false`，**整条 BUFF 静默不生效** → 属性只在一处写
- 跳转脚本页（GOTO/#INCLUDE）到含 GIVEBUFF 的页同样执行

### 6.3 GM 命令

| 命令 | 用法 | 说明 |
|---|---|---|
| @GIVEBUFF | `@GIVEBUFF <名字或槽位> [秒数] [玩家名]` | 给自己或指定玩家加 BUFF（PlayerObject.cs:3096） |
| @CUSTOMBUFF | `@CUSTOMBUFF` | 查看已配置的自定义 BUFF 清单 |
| @RELOADBUFF | `@RELOADBUFF` | 重载 BuffSystem.ini（日志显示「BUFF 配置已重新加载」） |

### 6.4 代码内置（约 120 处 AddBuff 调用）

| 来源 | 位置 | 典型 BUFF |
|---|---|---|
| 玩家技能 | HumanObject.cs（约 40 处）、HeroObject.cs（15 处） | 隐身术、轻身步、魔法盾、神圣战甲术、幽灵盾、无极真气、体迅风、先天气功、万效符系列、影化连击、灭杀窗口等 |
| 怪物 AI | MirObjects\Monsters\ 下 17 个文件（HornedArcher、GeneralMeowMeow、HornedWarrior、ChieftainSword、Riklebites、RhinoPriest、ReaperPriest、PowerBead、NobleWarrior、Mon562N/571N/573N、Butcher、ShardGuardian 等）+ MonsterObject 通用 | 各怪物护盾/印记/减益（50~70 号段） |
| 装备/称号/系统 | HumanObject 装备与状态刷新段 | 技巧项链、隐身戒指、英雄灵气、公会特效、新人特效、心心相映、四系绝技、精神状态 |
| 地图机制 | MirEnvir\Map.cs（4 处） | 安息之气、远古气息、华丽雨光、龙之特效/龙的特效（地图增益区） |

---

## 七、运维要点与常见坑

1. **新增枚举值必须同步注册**：BuffType 加新值后必须同时在 `BuffInfo.Load()` 注册，否则 AddBuff 直接抛 NotImplementedException（客户端/服务端断线）。
2. **自定义槽位只有 24 个**（Custom1~24，byte 上限），BuffSystem.ini 的 `[数字]` 节只认 1~24。
3. **属性只写一处**：脚本 GIVEBUFF 的追加属性与 SetBuffs.txt 行内属性冲突时整条失败且无提示。
4. **改 BuffSystem.ini 用 @RELOADBUFF**；改 SetBuffs.txt / NPC 脚本用 @RELOADNPCS 只重读脚本，SetBuffs 是每次 GIVEBUFF 执行时实时读文件，改完即生效。
5. **已有 BUFF 覆盖保护**：BuffSystem.ini 里覆盖已有 BUFF 时，没写的字段不会回退默认值（HasName/HasIcon/HasVisible/HasStack/HasProperty 五个哨兵位保证）。
6. **数据库存档**：Buff 随角色存档序列化（版本号 88 分界），改 BuffInfo 注册表不影响已存档的实例读取。
7. **攻速时长需求**：1 小时攻速 = 药水 Shape=3、Durability=60、属性页填「攻击速度」；或 NPC 脚本 `GIVEBUFF 攻速水 3600 False True False`（数值改 ini 后 @RELOADBUFF）。
