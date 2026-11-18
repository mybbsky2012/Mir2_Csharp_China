"""扫描另一账号(c6ba25d5)的会话存档，输出每个会话的任务状态与最后进展。只读。"""
import json, os, glob, datetime

BASE = r"C:\Users\86191\.workbuddy"
SNAP = os.path.join(BASE, r"c6ba25d5-6cb4-47ed-90d2-d898711c7807\sidebar-list-snapshot.json")


def ts(ms):
    return datetime.datetime.fromtimestamp(ms / 1000).strftime("%Y-%m-%d %H:%M")


def tail_messages(jsonl, max_bytes=400_000):
    """从文件尾部读，返回倒数若干条 message 记录（避免加载 20MB+ 全文）"""
    size = os.path.getsize(jsonl)
    start = max(0, size - max_bytes)
    with open(jsonl, "rb") as f:
        f.seek(start)
        raw = f.read()
    lines = raw.decode("utf-8", "ignore").split("\n")
    if start > 0:
        lines = lines[1:]
    out = []
    for line in lines:
        line = line.strip()
        if not line:
            continue
        try:
            j = json.loads(line)
        except Exception:
            continue
        if j.get("type") == "message":
            out.append(j)
    return out


def text_of(msg):
    c = msg.get("content")
    if isinstance(c, str):
        return c
    if isinstance(c, list):
        parts = []
        for p in c:
            if isinstance(p, dict):
                parts.append(p.get("text") or p.get("content") or "")
        return "\n".join(x for x in parts if isinstance(x, str))
    return ""


def main():
    snap = json.load(open(SNAP, encoding="utf-8"))
    for it in snap["items"]:
        cid = it["id"]
        space = (it.get("space") or {}).get("name", "")
        # 目录名规则：盘符小写 + "-" + 其余路径分隔符统一为 "-"
        p = space.replace("\\", "/")
        drive, rest = p.split(":", 1) if ":" in p else ("", p)
        slug = (drive.lower() + "-" + rest.lstrip("/")).replace("/", "-")
        jsonl = os.path.join(BASE, "projects", slug, cid + ".jsonl")
        print("=" * 90)
        print(f"# {it['title']}")
        print(f"  会话ID {cid[:8]} | 最后活动 {ts(it.get('lastActivityAt', 0))} | 工作区 {space}")
        print(f"  工作区存在: {os.path.isdir(space)} | 转录: {'有' if os.path.exists(jsonl) else '无'}"
              + (f" ({os.path.getsize(jsonl)//1024}KB)" if os.path.exists(jsonl) else ""))

        # 任务清单
        tdir = os.path.join(BASE, "tasks", cid)
        if os.path.isdir(tdir):
            for f in sorted(glob.glob(tdir + "/*.json"), key=lambda x: int(os.path.basename(x)[:-5])):
                j = json.load(open(f, encoding="utf-8"))
                if j.get("status") != "completed":
                    print(f"    [{j.get('status')}] {j.get('subject')}")

        if not os.path.exists(jsonl):
            continue
        msgs = tail_messages(jsonl)
        users = [m for m in msgs if m.get("role") == "user"]
        asst = [m for m in msgs if m.get("role") == "assistant" and text_of(m).strip()]
        if users:
            t = " ".join(text_of(users[-1]).split())
            print(f"  最后用户: {t[:300]}")
        if asst:
            t = " ".join(text_of(asst[-1]).split())
            print(f"  最后回复: {t[:500]}")


if __name__ == "__main__":
    main()
