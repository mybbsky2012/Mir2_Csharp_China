using Client.MirGraphics;

namespace Client.MirObjects
{
    /// <summary>
    /// 「人物简装」用的固定外观（内挂第 3 页），纯客户端表现层。
    ///
    /// 为什么布衣用 **1 号库**而不是数据库里「布衣(男)」的 Image=60：
    ///   这套客户端/微端资源里衣服库只有 00~58（CArmour 共 59 个文件），根本没有 60/80；
    ///   实测把 CArmour/00..06 的站立帧全导出来比对过 —— 00 是裸体内衣，01 才是灰白粗布衣
    ///   （男女两段都在同一个库里，靠 808 偏移区分），02 起就是彩色衣服/皮甲/重甲了。
    ///
    /// 帧数实测（Tools 里的 lib 探针）：
    ///   CArmour 每个库 1616 帧 = 男装 0..807 + 女装 808..1615；
    ///   玩家动作表 FrameSet.Player 最大帧号 511，所以
    ///   男 0+511=511 &lt; 1616 ✓ 、女 808+511=1319 &lt; 1616 ✓ —— 男女都不会缺帧。
    ///   CWeapon/30（木剑，实测导图确认是木质长剑）只有 832 帧，
    ///   女装段 416 偏移 + 511 = 927 会越界，所以简装时武器偏移统一按男性段 0 算
    ///   （木剑男女外观完全一样）。
    /// </summary>
    public static class PlayerAppearance
    {
        /// <summary>布衣在图库里的索引：01 号库（男女两段都在这一个库里，靠 808 偏移区分）。</summary>
        public const int PlainArmour = 1;

        /// <summary>木剑在图库里的索引：数据库实测 木剑 Image=30。</summary>
        public const int PlainSword = 30;

        /// <summary>取衣服库；超出长度就退回 0 号（与原版一致）。</summary>
        public static MLibrary ArmourLib(MLibrary[] libs, int image)
        {
            if (libs == null || libs.Length == 0) return null;

            return image >= 0 && image < libs.Length ? libs[image] : libs[0];
        }

        /// <summary>取木剑库；库里没有 30 号就用 0 号兜底。</summary>
        public static MLibrary SwordLib(MLibrary[] libs)
        {
            if (libs == null || libs.Length == 0) return null;

            return PlainSword < libs.Length ? libs[PlainSword] : libs[0];
        }
    }

    /// <summary>
    /// 「怪物简装」：把任何怪物都画成稻草人（Monster.Scarecrow = 27）。
    ///
    /// 只换绘制用的图库 + 帧号，**不碰 BodyLibrary/Frames/ActionFeed 状态机**：
    /// 对象的位置、排队动作、受击闪白、名字血条全部照旧，只是「画出来长什么样」变了。
    /// 做成纯绘制替换的好处：勾上/取消立刻生效，不用重建对象，也不会因为某只怪缺动作帧而消失。
    ///
    /// 帧号算法：稻草人库是老的 ver2 库（没有内嵌帧表），走 FrameSet.DefaultMonster，
    /// 站立段是「start 0 / 每方向 4 帧」。所以任意方向任意时刻都能算出一个 0~31 的合法帧，
    /// 不会出现「换库后帧号越界 → 整只怪不画」的问题（这是直接换 BodyLibrary 最容易踩的坑）。
    /// 实测 027.Lib 共 234 帧，装得下 DefaultMonster 全部动作段（最大 223）。
    ///
    /// 例外：卫士 / 弓箭手 / 练功师 —— 这些是官方的守卫型怪物，玩家要靠外形认它们，保持原样。
    /// （练功师在数据库里就是 KatanaGuard 的外形，AI=None、专门用来试伤害）
    /// </summary>
    public static class MonsterAppearance
    {
        private static MLibrary _scarecrow;
        private static int _scarecrowCount = -1;

        /// <summary>该怪是否保持原样（守卫型怪物）。</summary>
        public static bool KeepOriginal(ushort baseImage)
        {
            switch ((Monster)baseImage)
            {
                case Monster.Guard:          // 卫士 / 卫士0 / 卫士1
                case Monster.TaoistGuard:    // 道士守卫
                case Monster.Guard2:
                case Monster.Guard3:         // 卫士3
                case Monster.ArcherGuard:    // 弓箭手
                case Monster.ArcherGuard2:
                case Monster.ArcherGuard3:
                case Monster.KatanaGuard:    // 练功师
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>稻草人图库（微端没下完时返回 null，下一帧会自动重试）。</summary>
        public static MLibrary ScarecrowLib()
        {
            if (_scarecrow != null) return _scarecrow;

            MLibrary[] libs = Libraries.Monsters;
            int index = (ushort)Monster.Scarecrow;

            if (libs == null || index >= libs.Length) return null;

            _scarecrow = libs[index];
            return _scarecrow;
        }

        /// <summary>稻草人库的实际帧数（读不到时返回 0 —— 微端还没下完，下一帧再问）。</summary>
        public static int ScarecrowFrameCount()
        {
            if (_scarecrowCount > 0) return _scarecrowCount;

            MLibrary lib = ScarecrowLib();
            int count = lib == null ? 0 : lib.ImageCount;

            if (count > 0) _scarecrowCount = count;   // 只有真读到才缓存，别把「还没下完」钉死

            return count;
        }

        /// <summary>
        /// 算出「站立动作、指定方向、第 frameIndex 帧」在稻草人库里的帧号。
        /// 返回值恒为合法帧号（0~31），返回 -1 表示这一帧不替换（画原样，通常是微端还没下完）。
        /// </summary>
        public static int StandingFrame(int direction, int frameIndex)
        {
            Frame stand;
            if (!FrameSet.DefaultMonster.TryGetValue(MirAction.站立动作, out stand) || stand == null)
                return -1;

            int count = stand.Count > 0 ? stand.Count : 1;
            int dir = direction & 7;
            int offset = stand.Count + stand.Skip;

            int frame = stand.Start + offset * dir + (frameIndex % count);

            int total = ScarecrowFrameCount();

            // 库还没就绪（微端第一次遇到、正在后台下载）→ 返回 -1，这一帧画原样。
            // 如果这里放行，Draw 会静默失败，整屏怪物会「集体消失」到下载完成。
            if (total <= 0) return -1;

            if (frame >= total) return -1;

            return frame;
        }
    }
}
