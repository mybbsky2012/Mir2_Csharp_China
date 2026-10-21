# -*- coding: utf-8 -*-
"""生成《一键回收令》NPC 脚本。

套装分组、页面文案都在这里改，改完重跑一次即可重新生成脚本文件。
输出：Envir\\SystemScripts\\00Default\\特殊消耗品\\一键回收令.txt（UTF-8 BOM + LF）
"""
import os

OUT = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\Envir\SystemScripts\00Default\特殊消耗品\一键回收令.txt"

USE_ITEM_SHAPE = 80  # 物品 Shape，[@_UseItem(80)]

# 组名, 套装清单 [(显示名, ItemSet 枚举值, 枚举名)]
GROUPS = [
    ("初级套装", [
        ("白骨套装", 13, "白骨套装"),
        ("世轮套装", 9, "世轮套装"),
        ("绿翠套装", 10, "绿翠套装"),
        ("道护套装", 11, "道护套装"),
        ("祈祷套装", 1, "祈祷套装"),
        ("赤兰套装", 3, "赤兰套装"),
        ("密火套装", 4, "密火套装"),
        ("破碎套装", 5, "破碎套装"),
        ("幻魔石套", 6, "幻魔石套"),
    ]),
    ("中级套装", [
        ("灵玉套装", 7, "灵玉套装"),
        ("五玄套装", 8, "五玄套装"),
        ("红玉套装", 17, "红玉套装"),
        ("强红玉套", 18, "强红玉套"),
        ("软玉套装", 19, "软玉套装"),
        ("强软玉套", 20, "强软玉套"),
        ("白金套装", 15, "白金套装"),
        ("强白金套", 16, "强白金套"),
        ("青玉套装", 31, "青玉套装"),
    ]),
    ("高级套装", [
        ("强青玉套", 39, "强青玉套"),
        ("虫血套装", 14, "虫血套装"),
        ("记忆套装", 2, "记忆套装"),
        ("鏃未套装", 38, "鏃未套装"),
        ("龙血套装", 26, "龙血套装"),
        ("监视套装", 27, "监视套装"),
        ("暴压套装", 28, "暴压套装"),
        ("贝玉套装", 29, "贝玉套装"),
        ("黑术套装", 30, "黑术套装"),
    ]),
    ("顶级套装", [
        ("贵人战套", 21, "贵人战套"),
        ("贵人法套", 22, "贵人法套"),
        ("贵人道套", 23, "贵人道套"),
        ("贵人刺套", 24, "贵人刺套"),
        ("贵人弓套", 25, "贵人弓套"),
        ("天龙套装", 12, "天龙套装"),
        ("圣龙套装", 40, "圣龙套装"),
        ("神龙套装", 41, "神龙套装"),
    ]),
]

# 按「名字关键词」回收的散件分组（本服没做成 ItemSet 的经典系列）
# 显示名, 关键词（多个用 | 分隔，名字命中任意一个即回收）
KEYWORD_GROUPS = [
    ("沃玛系列", "沃玛"),
    ("祖玛系列", "祖玛"),
    ("赤月系列", "赤月"),
]

HEAD = """;=====================================================================
; 一键回收令 —— 一键回收（按套装 / 按名字）
;---------------------------------------------------------------------
; 三种触发方式，进的都是同一个菜单：
;   1) 背包里双击「一键回收令」物品（特殊消耗品，Shape = %d）
;   2) 聊天栏输入  @一键回收
;   3) 背包界面顶部的「一键回收」按钮（客户端内置，发的就是 @一键回收）
;
; 回收逻辑在服务端代码里（Server\\MirObjects\\RecycleManager.cs），本脚本只负责
; 菜单和确认。两种回收方式：
;   A. 按套装 —— RECYCLE <ItemSet枚举名>，只收属于该套装（物品表 Set 字段）的装备
;   B. 按名字 —— RECYCLEKEY <关键词>，只收名字里含该关键词的「装备类」物品
;      （沃玛 / 祖玛 / 赤月在本服没做成套装，只能按名字认；武器/盔甲/头盔/
;        项链/手镯/戒指/腰带/靴子 8 类收，任务物品、杂物、消耗品一律不碰）
;
; 两种方式共用的规则：
;   · 只收背包（含腰带 0~5 格），装备栏、仓库、精炼台一律不碰
;   · 回收价 = 物品数据库原价 × 数量，不打折
;   · 绑定「不可出售」的物品自动跳过
;   · 物品直接销毁，不进商店的「买回」列表
;
; ★ 想增减按套装收的：改下面 GROUPS，重跑本脚本
; ★ 想增减按名字收的：改下面 KEYWORD_GROUPS，重跑本脚本
; ★ RECYCLE 后面那个参数必须和 Shared\\Enums.cs 里 ItemSet 的枚举名一字不差
; ★ RECYCLEKEY 的关键词多个用 | 分隔，命中任意一个即回收
;=====================================================================


;;------------ 物品触发：一键回收令（Shape = %d）------------
[@_UseItem(%d)]
#ACT
GOTO @一键回收


;;------------ 聊天命令触发：@一键回收 ------------
[@一键回收]
#SAY
背包塞满了吧？成套的装备别一件件丢，我可以直接帮你换成金币。\\
回收价按物品原价算，只收背包里的东西，身上穿的、仓库里的都不动。\\
前两行是按套装收，最下面一行是按名字收（沃玛、祖玛、赤月这类散件）：\\
<初级套装/@回收目录1> <中级套装/@回收目录2> <高级套装/@回收目录3> <顶级套装/@回收目录4>\\
<沃玛系列/@回收词1> <祖玛系列/@回收词2> <赤月系列/@回收词3>\\
<关闭/@exit>
"""

TAIL = """

;;------------ 回收结果页 ------------
[@回收完成]
#SAY
处理完了，回收到的金币已经放进背包。\\
还想清别的套装就继续选，不想清了就关掉。\\
<继续回收/@一键回收> <关闭/@exit>
"""


def build():
    out = [HEAD % (USE_ITEM_SHAPE, USE_ITEM_SHAPE, USE_ITEM_SHAPE)]

    for gi, (gname, items) in enumerate(GROUPS, start=1):
        out.append("")
        out.append(";;------------ %s 目录 ------------" % gname)
        out.append("[@回收目录%d]" % gi)
        out.append("#SAY")
        out.append("%s里这些我能收，点名字进去确认：\\" % gname)

        # 每行 3 个按钮
        for i in range(0, len(items), 3):
            row = items[i:i + 3]
            line = " ".join("<%s/@回收%d>" % (n, v) for n, v, _ in row)
            out.append(line + "\\")

        out.append("<返回/@一键回收> <关闭/@exit>")

    for gi, (gname, items) in enumerate(GROUPS, start=1):
        for name, val, enum_name in items:
            out.append("")
            out.append(";;------------ %s（ItemSet = %d）------------" % (name, val))
            out.append("[@回收%d]" % val)
            out.append("#SAY")
            out.append("【%s】\\" % name)
            out.append("背包里所有【%s】的装备都会换成金币，价格按物品原价算。\\" % name)
            out.append("确认后不能反悔，装备也拿不回来，想清楚再点。\\")
            out.append("<确定回收/@执行%d> <返回/@回收目录%d>" % (val, gi))
            out.append("")
            out.append("[@执行%d]" % val)
            out.append("#ACT")
            out.append("RECYCLE %s" % enum_name)
            out.append("GOTO @回收完成")

    # 按名字关键词回收的分组（沃玛 / 祖玛 / 赤月）：确认页 + 执行页
    for ki, (gname, kw) in enumerate(KEYWORD_GROUPS, start=1):
        out.append("")
        out.append(";;------------ %s（按名字收，关键词：%s）------------" % (gname, kw))
        out.append("[@回收词%d]" % ki)
        out.append("#SAY")
        out.append("【%s】\\" % gname)
        out.append("背包里名字带「%s」的武器、盔甲、头盔、项链、手镯、戒指、腰带、靴子，\\" % kw)
        out.append("全部按物品原价换成金币；任务物品、杂物、消耗品这些不碰。\\")
        out.append("确认后不能反悔，装备也拿不回来，想清楚再点。\\")
        out.append("（背包里没有这类装备时会直接提示，不会扣任何东西）\\")
        out.append("<确定回收/@执行词%d> <返回/@一键回收>" % ki)
        out.append("")
        out.append("[@执行词%d]" % ki)
        out.append("#ACT")
        out.append("RECYCLEKEY %s" % kw)
        out.append("GOTO @回收完成")

    out.append(TAIL)

    return "\n".join(out) + "\n"

if __name__ == "__main__":
    text = build()
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8-sig", newline="\n") as f:
        f.write(text)

    total = sum(len(x[1]) for x in GROUPS)
    print("已生成:", OUT)
    print("套装分组 %d 个、套装 %d 个；按名字分组 %d 个，页面 %d 个"
          % (len(GROUPS), total, len(KEYWORD_GROUPS),
             1 + len(GROUPS) + total * 2 + len(KEYWORD_GROUPS) * 2 + 1))
