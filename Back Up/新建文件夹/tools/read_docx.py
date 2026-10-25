"""最小依赖读取 .docx（zip + xml），支持段落与表格。"""
import zipfile, re, sys
from xml.etree import ElementTree as ET

NS = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}


def _text(el):
    parts = []
    for t in el.iter():
        tag = t.tag.split('}')[-1]
        if tag == 't':
            parts.append(t.text or '')
        elif tag == 'tab':
            parts.append('\t')
        elif tag == 'br':
            parts.append('\n')
    return ''.join(parts)


def read(path):
    z = zipfile.ZipFile(path)
    xml = z.read('word/document.xml')
    root = ET.fromstring(xml)
    body = root.find('w:body', NS)
    out = []
    for child in body:
        tag = child.tag.split('}')[-1]
        if tag == 'p':
            txt = _text(child).strip()
            if txt:
                out.append(('P', txt))
        elif tag == 'tbl':
            rows = []
            for tr in child.findall('w:tr', NS):
                cells = [_text(tc).strip() for tc in tr.findall('w:tc', NS)]
                rows.append(cells)
            out.append(('T', rows))
    return out


if __name__ == '__main__':
    path = sys.argv[1] if len(sys.argv) > 1 else 'x.docx'
    kw = sys.argv[2] if len(sys.argv) > 2 else None
    for kind, val in read(path):
        if kind == 'P':
            if kw and kw not in val:
                continue
            print(val)
        else:
            for r in val:
                line = ' | '.join(r)
                if kw and kw not in line:
                    continue
                print('   ', line)
