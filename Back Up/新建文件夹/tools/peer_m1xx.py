import os, re, hashlib, collections
BS = chr(92)
CLI = r"E:\micromir2\Sound"
h2f = collections.defaultdict(list)
for f in os.listdir(CLI):
    p = os.path.join(CLI, f)
    if os.path.isfile(p) and os.path.splitext(f)[1].lower() in (".wav", ".mp3"):
        h2f[hashlib.md5(open(p, 'rb').read()).hexdigest()].append(f)
def key(f):
    m = re.match(r'^M(\d+)-(\d+)\.', f, re.I)
    return (int(m.group(1)), int(m.group(2))) if m else (9999, 9)
for f in sorted([x for x in os.listdir(CLI) if re.match(r'^M1\d\d-', x, re.I)], key=key):
    p = os.path.join(CLI, f)
    h = hashlib.md5(open(p, 'rb').read()).hexdigest()
    peers = sorted([x for x in h2f[h] if x.lower() != f.lower()], key=key)
    print(f"{f:14s} {os.path.getsize(p):8d}  peers={peers if peers else '—'}")
