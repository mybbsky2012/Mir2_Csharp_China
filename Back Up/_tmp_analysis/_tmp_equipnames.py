# -*- coding: utf-8 -*-
import csv

p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\Exports\2_物品数据.csv"
with open(p, encoding='utf-8-sig', newline='') as f:
    rows = list(csv.DictReader(f))

CLS = {"战士": 1, "法师": 2, "道士": 4, "刺客": 8, "弓箭": 16,
       "战法道": 7, "全职业": 31, "无": 31, "": 31}

slot_types = ['武器', '盔甲', '头盔', '项链', '手镯', '戒指', '腰带', '靴子', '守护石']

def score(x):
    def i(k):
        try: return int(str(x.get(k, "")).strip() or 0)
        except Exception: return 0
    return i("ItemStatMaxDC") + i("ItemStatMaxMC") + i("ItemStatMaxSC") + i("ItemStatMaxAC")*2 + i("ItemStatMaxMAC")*2 + i("ItemStatHP")/2

for cls in ["战士", "法师", "道士", "刺客", "弓箭"]:
    mask = CLS[cls]
    print(f'========== {cls} ==========')
    for st in slot_types:
        cands = []
        for x in rows:
            if x['ItemType'].strip() != st: continue
            key = x.get('ItemRequiredClass', '').strip()
            m = CLS.get(key)
            if m is None:
                # 未知组合名，尝试包含
                m = 0
                for k, v in CLS.items():
                    if k and k in key: m |= v
            if (m & mask) == 0: continue
            if x.get('ItemRequiredType', '').strip() != 'Level': continue
            try: amt = int(x.get('ItemRequiredAmount', '0').strip() or 0)
            except Exception: amt = 0
            if not (30 <= amt <= 45): continue
            g = x.get('ItemRequiredGender', '').strip()
            cands.append((score(x), x['ItemName'].strip(), amt, g))
        cands.sort(reverse=True)
        tops = [f"{n}(L{a}{'/'+g if g and g not in ('无','') else ''})" for _, n, a, g in cands[:4]]
        print(f'  {st}: {"  ".join(tops) if tops else "（无）"}')
