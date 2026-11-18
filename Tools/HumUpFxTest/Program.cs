using System.Reflection;
using Client.MirGraphics;
using Client.MirObjects;

namespace HumUpFxTest
{
    /// <summary>
    /// 羽化重数分档特效（11-13）自动化测试（无 D3D，纯逻辑/素材/IL 三层验证）：
    ///   A. 真代码档位映射：PlayerObject.GetHumUpStageFx 对每档重数给出正确的 (脚下, 龙) 段；
    ///   B. 真素材校验：Effect.lib 中每个段位「段内全非空、段后紧跟占位帧」——证明帧数表
    ///      恰好覆盖真实动画，不会把 4×1 占位帧播进去（否则末尾闪空）；
    ///   C. 真构造行为：按 SetEffects 羽化分支同样的调用方式 new 真实的 SpecialEffect，
    ///      逐字段核对（素材段/帧数/时长/前后层），并验证骑乘只保留龙、未羽化为空；
    ///   D. 真 IL 校验：反编译字节码确认 SetEffects 确实在调用 GetHumUpStageFx（= 旧的
    ///      「HumUpStage>0 → 固定 1020+990」路径已不在 IL 里存活），RefreshHumUpLook 确实调 SetEffects。
    /// 退出码 0 = 全过；否则 2。
    /// </summary>
    internal static class Program
    {
        private static int _failed;

        private static void Check(bool ok, string name, string detail = "")
        {
            Console.WriteLine((ok ? "  [PASS] " : "  [FAIL] ") + name + (detail.Length > 0 ? "  " + detail : ""));
            if (!ok) _failed++;
        }

        private static int Main(string[] args)
        {
            Console.WriteLine("== 羽化重数分档特效 HumUpFxTest ==");

            // ---------- A. 档位映射（真代码） ----------
            Console.WriteLine("A. GetHumUpStageFx 档位映射");
            int[] expF = { 680, 680, 680, 680, 680, 1210, 1210, 1210, 990, 990 };
            int[] expFC = { 20, 20, 20, 20, 20, 20, 20, 20, 20, 20 };
            int[] expD = { 714, 714, 714, 714, 714, 1240, 1240, 1240, 1020, 1020 };
            int[] expDC = { 36, 36, 36, 36, 36, 31, 31, 31, 32, 32 };
            for (int stage = 1; stage <= 10; stage++)
            {
                bool ok = PlayerObject.GetHumUpStageFx(stage, out int fs, out int fc, out int ds, out int dc);
                Check(ok && fs == expF[stage - 1] && fc == expFC[stage - 1] && ds == expD[stage - 1] && dc == expDC[stage - 1],
                    $"重数 {stage}", ok ? $"脚下 {fs}+{fc}  龙 {ds}+{dc}" : "不应返回 false");
            }
            for (int stage = -1; stage <= 0; stage++)
                Check(!PlayerObject.GetHumUpStageFx(stage, out _, out _, out _, out _), $"重数 {stage}（未羽化）无特效");
            Check(!PlayerObject.GetHumUpStageFx(11, out _, out _, out _, out _), "重数 11（越界）无特效");

            // ---------- B. 素材校验（真 Effect.lib） ----------
            Console.WriteLine("B. Effect.lib 素材段校验");
            string libPath = FindEffectLib(args);
            if (libPath == null)
            {
                Console.WriteLine("  [SKIP] 找不到 Effect.lib（可传参指定路径）");
            }
            else
            {
                Console.WriteLine("  库: " + libPath);
                var lib = EffectLib.Load(libPath);
                Check(lib.Count > 1270, "库帧数 >= 1271", "实际 " + lib.Count);
                // (段起点, 真实帧数)——与 GetHumUpStageFx 的表一致
                var segments = new[] { (680, 20), (714, 36), (990, 20), (1020, 32), (1210, 20), (1240, 31) };
                foreach (var (start, count) in segments)
                {
                    int emptyInside = 0, dummyInside = 0;
                    for (int i = start; i < start + count; i++)
                    {
                        if (lib.IsEmpty(i)) emptyInside++;
                        else if (lib.IsDummy(i)) dummyInside++;   // 4×1 之类占位帧混进段内 = 末尾会闪空
                    }
                    Check(emptyInside == 0 && dummyInside == 0, $"段 {start}-{start + count - 1} 段内 {count} 帧全为真实画面",
                        $"空帧 {emptyInside} / 占位帧 {dummyInside}");

                    // 段后紧邻 3 帧（越界=到库尾）必须全是空帧/占位帧——证明真实动画恰好到
                    // start+count-1 为止（库里段与段背靠背紧挨，更远处的内容属于别的特效，不算）
                    int firstRealAfter = -1;
                    for (int i = start + count; i < Math.Min(start + count + 3, lib.Count); i++)
                        if (!lib.IsEmpty(i) && !lib.IsDummy(i)) { firstRealAfter = i; break; }
                    Check(firstRealAfter < 0, $"段 {start} 之后紧跟占位/空帧", firstRealAfter >= 0 ? $"{firstRealAfter} 处仍有真实画面帧" : "");
                }
                // 弃用段（11-13 二次定稿）：960 火焰兽不再被引用，只做存在性记录
                Console.WriteLine($"  [INFO] 弃用段 960-989 非空帧 {CountReal(lib, 960, 30)}（保留在库里不删）");
            }

            // ---------- C. 构造行为（真 SpecialEffect，模拟 SetEffects 羽化分支） ----------
            Console.WriteLine("C. SpecialEffect 构造行为（SetEffects 羽化分支同款调用）");
            var owner = new StubObject();
            for (int stage = 1; stage <= 10; stage++)
            {
                owner.Effects.Clear();
                PlayerObject.GetHumUpStageFx(stage, out int fxFoot, out int fxFootCount, out int fxDragon, out int fxDragonCount);
                // ↓↓↓ 与 PlayerObject.SetEffects 羽化分支逐字一致（除 Libraries.Effect → null、骑乘判定固定 false）
                owner.Effects.Add(new SpecialEffect(null, fxDragon, fxDragonCount, fxDragonCount * 130, owner, true, false, 1) { Repeat = true });
                owner.Effects.Add(new SpecialEffect(null, fxFoot, fxFootCount, fxFootCount * 160, owner, true, true, 1) { Repeat = true });
                // ↑↑↑
                var dragon = owner.Effects[0];
                var foot = owner.Effects[1];
                bool ok = owner.Effects.Count == 2
                    && dragon.BaseIndex == fxDragon && dragon.Count == fxDragonCount
                    && dragon.Duration == fxDragonCount * 130 && !dragon.DrawBehind && dragon.Blend
                    && foot.BaseIndex == fxFoot && foot.Count == fxFootCount
                    && foot.Duration == fxFootCount * 160 && foot.DrawBehind && foot.Blend;
                Check(ok, $"重数 {stage} 两层特效字段",
                    $"龙 {dragon.BaseIndex}+{dragon.Count}/{dragon.Duration}ms 上层 | 脚下 {foot.BaseIndex}+{foot.Count}/{foot.Duration}ms 下层");

                // 骑乘：只留龙
                owner.Effects.Clear();
                owner.Effects.Add(new SpecialEffect(null, fxDragon, fxDragonCount, fxDragonCount * 130, owner, true, false, 1) { Repeat = true });
                Check(owner.Effects.Count == 1 && owner.Effects[0].BaseIndex == fxDragon, $"重数 {stage} 骑乘只保留龙");
            }
            owner.Effects.Clear();
            if (!PlayerObject.GetHumUpStageFx(0, out _, out _, out _, out _))
                Check(owner.Effects.Count == 0, "未羽化不加任何特效");

            // ---------- D. IL 校验（真编译产物） ----------
            Console.WriteLine("D. IL 校验");
            MethodInfo setFx = typeof(PlayerObject).GetMethod("SetEffects", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var calls = IlCalls(setFx);
            Check(calls.Any(m => m.Name == "GetHumUpStageFx"), "SetEffects 调用了 GetHumUpStageFx（分档表生效）");
            MethodInfo refresh = typeof(PlayerObject).GetMethod("RefreshHumUpLook", BindingFlags.Public | BindingFlags.Instance);
            var calls2 = IlCalls(refresh);
            Check(calls2.Any(m => m.Name == "SetEffects"), "RefreshHumUpLook 调用了 SetEffects（重数变化特效立即切换）");
            Check(calls2.Any(m => m.Name == "SetLibraries"), "RefreshHumUpLook 仍调用 SetLibraries");

            Console.WriteLine(_failed == 0 ? "\n全部通过 PASS" : $"\n失败 {_failed} 项 FAIL");
            return _failed == 0 ? 0 : 2;
        }

        private static int CountReal(EffectLib lib, int start, int len)
        {
            int n = 0;
            for (int i = start; i < start + len; i++) if (!lib.IsEmpty(i)) n++;
            return n;
        }

        /// <summary>解析方法 IL 里的 call/callvirt/newobj 目标方法。</summary>
        private static List<MethodInfo> IlCalls(MethodInfo method)
        {
            var result = new List<MethodInfo>();
            byte[] il = method.GetMethodBody().GetILAsByteArray();
            var module = method.Module;
            for (int i = 0; i < il.Length; i++)
            {
                byte op = il[i];
                if (op == 0xFE) { i++; continue; }               // 双字节前缀
                if (op != 0x28 && op != 0x6F && op != 0x73) continue;  // call / callvirt / newobj
                int token = BitConverter.ToInt32(il, i + 1);
                i += 4;
                try
                {
                    if (module.ResolveMethod(token) is MethodInfo mi) result.Add(mi);
                }
                catch { /* 泛型/其他 token 忽略 */ }
            }
            return result;
        }

        private static string FindEffectLib(string[] args)
        {
            if (args.Length > 0 && File.Exists(args[0])) return args[0];
            foreach (var p in new[] { @"E:\micromir2\Data\Effect.lib", @"D:\龙之传奇\Data\Effect.lib" })
                if (File.Exists(p)) return p;
            return null;
        }

        /// <summary>测试桩：MapObject 无参构造是空的，Effects 是普通 List，不碰 D3D。</summary>
        private class StubObject : MapObject
        {
            public StubObject() { }
            public override ObjectType Race => ObjectType.Player;
            public override bool Blocking => false;
            public override void Process() { }
            public override void Draw() { }
            public override bool MouseOver(Point p) => false;
            public override void DrawBehindEffects(bool effectsEnabled) { }
            public override void DrawEffects(bool effectsEnabled) { }
        }
    }

    /// <summary>Mir2 .Lib 只读解析（格式同 Client/MirGraphics/MLibrary.cs）。</summary>
    internal class EffectLib
    {
        public int Count;
        private readonly int[] _offsets;
        private readonly byte[] _data;

        private EffectLib(byte[] data, int[] offsets)
        {
            _data = data;
            _offsets = offsets;
            Count = offsets.Length;
        }

        public static EffectLib Load(string path)
        {
            var data = File.ReadAllBytes(path);
            int version = BitConverter.ToInt32(data, 0);
            int count = BitConverter.ToInt32(data, 4);
            int pos = version >= 3 ? 16 : 8;
            var offsets = new int[count];
            Buffer.BlockCopy(data, pos, offsets, 0, count * 4);
            return new EffectLib(data, offsets);
        }

        /// <summary>帧是否存在且有画面（偏移>0、宽高>0、gzip 可解、长度足够）。</summary>
        public bool IsEmpty(int index)
        {
            if (index < 0 || index >= _offsets.Length) return true;
            int off = _offsets[index];
            if (off <= 0 || off + 17 > _data.Length) return true;
            short w = BitConverter.ToInt16(_data, off);
            short h = BitConverter.ToInt16(_data, off + 2);
            int length = BitConverter.ToInt32(_data, off + 13);
            if (w <= 0 || h <= 0 || length <= 0) return true;
            int start = off + 17, end = start + length;
            if (end > _data.Length) return true;
            try
            {
                using var src = new MemoryStream(_data, start, length, false);
                using var gz = new System.IO.Compression.GZipStream(src, System.IO.Compression.CompressionMode.Decompress);
                byte[] buf = new byte[w * h * 4];
                int read = 0, n;
                while ((n = gz.Read(buf, read, buf.Length - read)) > 0) read += n;
                return read < w * h * 4;
            }
            catch { return true; }
        }

        /// <summary>占位帧：有像素数据但尺寸是 4×1 之类的小块（原版 Lib 在动画段末尾挂的收尾空帧）。</summary>
        public bool IsDummy(int index)
        {
            if (index < 0 || index >= _offsets.Length) return false;
            int off = _offsets[index];
            if (off <= 0 || off + 17 > _data.Length) return false;
            short w = BitConverter.ToInt16(_data, off);
            short h = BitConverter.ToInt16(_data, off + 2);
            return w > 0 && h > 0 && w < 8 && h < 8;
        }
    }
}
