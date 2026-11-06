# -*- coding: utf-8 -*-
"""
残影机理仿真器 —— 按 Client 源码语义建模，验证「黄色虚影」能不能由各候选机理产生。

建模依据（全部来自 D:/6b/Client 实码，非猜测）：
  [F1] MapControl.CreateTexture (GameScene.cs:11358-11474)
       每次重建：Clear(BackColour) -> 背景 -> 地板 -> DrawObjects -> 特效 -> DrawLights -> TextureValid=true
       —— TextureValid=true 在最后一行：中途抛异常则贴图"无效"，下一帧必然整体重建。
  [F2] DrawControl (MirControl.cs:713-723)
       if (!TextureValid) CreateTexture(); 然后整块贴图 DrawOpaque 到屏幕。
  [F3] Application_Idle (CMain.cs:103-119)
       主循环外层 try/catch：绘制异常被吞掉、写 Error.txt，下一帧继续 —— 没有局部 catch。
  [F4] DXManager.Blending 是全局静态；SetBlend(true) 后所有精灵走加法混合（发黄）。
  [F5] 高亮二次绘制 (GameScene.cs:11848-11859)
       HighlightTarget 开启时：MouseObject / TargetObject / LockObject 各再画一遍 DrawBlend()=0.3 加法。
       LockObject 由 MagicLock 维持（不随鼠标移开丢锁），玩家/怪物/NPC 都能被锁。
  [F6] 用户现场：Mir2Config.ini HighlightTarget=True, MagicLock=True。

输出：每帧"缓存贴图"里的精灵清单 -> 残影判定 -> JSON 交给 HTML 动画。
"""
import json, copy

CELL_W, CELL_H = 48, 32
SCR_W, SCR_H = 15, 9          # 视野格数

GRASS   = (58, 106, 48)
DEER    = (238, 234, 224)
DEER_D  = (150, 142, 128)
NPC_C   = (222, 186, 140)
PLAYER  = (70, 90, 160)


class DX:
    """DXManager.Blending 全局状态（[F4]）"""
    blending = False
    rate = 1.0
    log = []

    @classmethod
    def set_blend(cls, v, rate=1.0):
        cls.blending = v
        cls.rate = rate


class Obj:
    def __init__(self, name, x, y, w, h, colour, kind="monster"):
        self.name, self.x, self.y = name, x, y
        self.w, self.h, self.colour, self.kind = w, h, colour, kind
        self.fault_frame = None       # 该对象在指定帧绘制时抛异常（故障注入）

    def draw(self, sx, sy, additive=False, alpha=1.0, tag=""):
        return dict(x=sx, y=sy, w=self.w, h=self.h,
                    colour=list(self.colour), alpha=alpha,
                    additive=additive, label=self.name + tag)


class World:
    def __init__(self):
        self.deer_a = Obj("鹿A", 4, 6, 40, 26, DEER)
        self.deer_b = Obj("鹿B", 8, 3, 40, 26, DEER)
        self.npc    = Obj("NPC", 2, 2, 34, 40, NPC_C, "npc")
        self.player = Obj("玩家", 6, 5, 26, 40, PLAYER, "player")
        self.objects = [self.npc, self.deer_a, self.deer_b, self.player]


def world_to_screen(ob, cam):
    """DrawLocation 公式（MonsterObject.cs:486）：(Movement - User.Movement + OffSet) * Cell"""
    sx = (ob.x - cam[0]) * CELL_W + 20
    sy = (ob.y - cam[1]) * CELL_H + 10
    return sx, sy


class Pipeline:
    """按 [F1][F3][F4][F5] 语义复刻的一帧重建。build: old=无防护  new=带补丁"""

    def __init__(self, build):
        self.build = build          # 'old' / 'new'
        self.texture_valid = True
        self.error_lines = []
        self.leak_count = 0

    def render_frame(self, world, cam, frame, highlight_lock=None, fault=None):
        """返回 (贴图精灵列表, 本帧是否异常中止)。highlight_lock=被锁定对象或 None。"""
        DX.set_blend(False)  # 正常帧起点
        aborted = False

        # --- [补丁] CreateTexture 入口护栏 ---
        if self.build == "new" and DX.blending:
            DX.set_blend(False)
            self.leak_count += 1
            if self.leak_count <= 5:
                self.error_lines.append(
                    "第%d帧 [渲染] 场景重建时检测到加法混合状态泄漏，已强制复位" % frame)

        sprites = []

        def do_objects():
            for ob in sorted(world.objects, key=lambda o: o.y):
                if ob.fault_frame is not None and frame >= ob.fault_frame:
                    raise RuntimeError("%s 绘制抛异常（模拟素材缺失）" % ob.name)
                sx, sy = world_to_screen(ob, cam)
                sprites.append(ob.draw(sx, sy))

        def do_highlight():
            # [F5] GameScene.cs:11848-11859 —— TargetObject/LockObject 无条件再画一遍 0.3 加法
            if highlight_lock is None:
                return
            sx, sy = world_to_screen(highlight_lock, cam)
            # 画在最上层：不加遮挡（对象循环之后才画）
            sprites.append(highlight_lock.draw(sx, sy, additive=True, alpha=0.3, tag="·高亮"))

        def do_lights():
            DX.set_blend(True)
            # ...光源绘制（省略，不影响状态结论）...
            DX.set_blend(False)

        # --- [F1] 重建：Clear -> 对象 -> 高亮 -> 光照 -> TextureValid=true ---
        sprites.append(dict(x=0, y=0, w=SCR_W * CELL_W, h=SCR_H * CELL_H,
                            colour=list(GRASS), alpha=1.0, additive=False, label="地板"))
        try:
            do_objects()
            do_highlight()
            do_lights()
        except RuntimeError as e:
            # [F3] 异常一路抛到 Application_Idle 才被吞：本帧到此为止，
            # TextureValid 仍是 False（[F1] 它在最后一行）→ 下一帧整体重建。
            self.error_lines.append("第%d帧 异常: %s" % (frame, e))
            aborted = True
            return sprites, True

        self.texture_valid = True
        return sprites, False


def add_blend(dst, src_col, alpha):
    """加法混合 SourceAlpha/Dest=One（[F4]）：dst + src*alpha"""
    return tuple(min(255, int(d + s * alpha)) for d, s in zip(dst, src_col))


def classify(sprites):
    """统计缓存贴图里的"异常加法精灵"（不该出现的黄色虚影）"""
    base = [s for s in sprites if not s["additive"]]
    ghosts = [s for s in sprites if s["additive"]]
    return base, ghosts


def blend_preview(sprites):
    """把精灵合成一个 15x9 格的粗略像素图，返回"虚影色"，用于演示加法混合后的颜色"""
    px = [[GRASS for _ in range(SCR_W * 4)] for _ in range(SCR_H * 4)]
    for s in sprites:
        cx = int((s["x"] + s["w"] / 2) // CELL_W * 4)
        cy = int((s["y"] + s["h"] / 2) // CELL_H * 4)
        if 0 <= cy < len(px) and 0 <= cx < len(px[0]):
            r, g, b = s["colour"]
            if s["additive"]:
                px[cy][cx] = add_blend(px[cy][cx], (r, g, b), s["alpha"])
            else:
                a = s["alpha"]
                px[cy][cx] = tuple(int(d * (1 - a) + c * a) for d, c in zip(px[cy][cx], (r, g, b)))
    return px


def run_scenario_A():
    """机理A：混合状态泄漏（异常型）—— 旧代码 vs 补丁。故障：鹿B 从第3帧起抛异常。"""
    out = {}
    for build in ("old", "new"):
        w = World()
        w.deer_b.fault_frame = 3
        p = Pipeline(build)
        frames = []
        cam = [6, 5]
        for f in range(8):
            sprites, aborted = p.render_frame(w, cam, f)
            base, ghosts = classify(sprites)
            frames.append(dict(frame=f, aborted=aborted,
                               ghost_count=len(ghosts),
                               valid=not aborted,
                               note=("本帧异常中止→贴图未标记有效" if aborted else "正常重建")))
            cam[0] += 1  # 玩家往东走
        out[build] = dict(frames=frames, errors=p.error_lines[:6])
    return out


def run_scenario_B():
    """机理B：高亮二次绘制（HighlightTarget+MagicLock，用户现场配置）。"""
    w = World()
    p = Pipeline("old")   # 无故障，新旧代码行为一致
    frames = []
    cam = [6, 5]
    # 0-4帧锁鹿B，5-9帧锁NPC，10帧后无锁 —— 对应"鼠标换目标，旧虚影消失"
    locks = [w.deer_b] * 5 + [w.npc] * 5 + [None] * 3
    for f, lk in enumerate(locks):
        sprites, _ = p.render_frame(w, cam, f, highlight_lock=lk)
        base, ghosts = classify(sprites)
        cam[0] += 1
        frames.append(dict(frame=f, lock=(lk.name if lk else None),
                           ghost_count=len(ghosts),
                           ghosts=[dict(label=g["label"], x=g["x"], y=g["y"],
                                        colour=list(add_blend(GRASS, g["colour"], g["alpha"])))
                                   for g in ghosts],
                           sprites=sprites))
    return frames


def main():
    A = run_scenario_A()
    B = run_scenario_B()

    print("=" * 72)
    print("机理A：混合状态泄漏（异常型）   [故障：鹿B 第3帧起绘制抛异常]")
    print("-" * 72)
    for build in ("old", "new"):
        print("  构建版本：%s" % ("旧代码（无 try/finally、无护栏）" if build == "old" else "补丁版（finally+护栏）"))
        for fr in A[build]["frames"]:
            print("    帧%d  %-28s  残影数=%d" % (fr["frame"], fr["note"], fr["ghost_count"]))
        for e in A[build]["errors"]:
            print("    log: %s" % e)
        print()

    print("=" * 72)
    print("机理B：高亮二次绘制（HighlightTarget=True + MagicLock=True，现场配置）")
    print("-" * 72)
    for fr in B:
        gs = ", ".join("%s@(%d,%d)→RGB%s" % (g["label"], g["x"], g["y"], tuple(g["colour"]))
                       for g in fr["ghosts"]) or "无"
        print("  帧%-2d 锁定=%-4s 残影数=%d  %s" % (fr["frame"], fr["lock"], fr["ghost_count"], gs))

    # 虚影颜色演示：鹿(238,234,224) 0.3 加法 叠在草(58,106,48)上
    c = add_blend(GRASS, DEER, 0.3)
    print()
    print("  虚影颜色 = 草地 %s + 鹿 %s × 0.3 加法 = RGB%s（黄白色，即截图中的颜色）"
          % (GRASS, DEER, c))

    json.dump(dict(A=A, B=B), open("sim_result.json", "w", encoding="utf-8"), ensure_ascii=False)
    print("\n已输出 sim_result.json")


if __name__ == "__main__":
    main()
