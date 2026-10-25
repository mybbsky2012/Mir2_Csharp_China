# -*- coding: utf-8 -*-
"""不依赖 openpyxl，用 zip+xml 读取 xlsx。"""
import zipfile, re, sys
import xml.etree.ElementTree as ET

NS = '{http://schemas.openxmlformats.org/spreadsheetml/2006/main}'


def col_to_idx(ref):
    m = re.match(r'([A-Z]+)', ref)
    c = 0
    for ch in m.group(1):
        c = c * 26 + (ord(ch) - 64)
    return c - 1


def read(path):
    z = zipfile.ZipFile(path)
    shared = []
    if 'xl/sharedStrings.xml' in z.namelist():
        root = ET.fromstring(z.read('xl/sharedStrings.xml'))
        for si in root.findall(NS + 'si'):
            shared.append(''.join(t.text or '' for t in si.iter(NS + 't')))
    wb = ET.fromstring(z.read('xl/workbook.xml'))
    rels = ET.fromstring(z.read('xl/_rels/workbook.xml.rels'))
    rid2t = {r.get('Id'): r.get('Target') for r in rels}
    out = []
    for sh in wb.find(NS + 'sheets'):
        name = sh.get('name')
        rid = sh.get('{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id')
        tgt = rid2t[rid].lstrip('/')
        if not tgt.startswith('xl/'):
            tgt = 'xl/' + tgt
        ws = ET.fromstring(z.read(tgt))
        rows = []
        for row in ws.iter(NS + 'row'):
            cells = {}
            for c in row.findall(NS + 'c'):
                ref = c.get('r') or ''
                ci = col_to_idx(ref) if ref else len(cells)
                t = c.get('t')
                v = c.find(NS + 'v')
                isv = c.find(NS + 'is')
                if t == 's' and v is not None:
                    val = shared[int(v.text)]
                elif t == 'inlineStr' and isv is not None:
                    val = ''.join(x.text or '' for x in isv.iter(NS + 't'))
                elif v is not None:
                    val = v.text
                else:
                    val = ''
                cells[ci] = val
            if cells:
                n = max(cells) + 1
                rows.append([cells.get(i, '') for i in range(n)])
            else:
                rows.append([])
        out.append((name, rows))
    return out


if __name__ == '__main__':
    for name, rows in read(sys.argv[1]):
        print('=' * 70)
        print('SHEET:', name, ' rows=', len(rows))
        lim = int(sys.argv[2]) if len(sys.argv) > 2 else 25
        for r in rows[:lim]:
            print('  ', ' | '.join(str(x) for x in r))
