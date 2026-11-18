namespace Client.MirObjects
{
    public class FrameSet : Dictionary<MirAction, Frame>
    {
        //★集合初始化器走这里：重复 key 覆盖而不是抛 ArgumentException。
        //  曾因 HumUpPlayer 里「站立姿势2」被 Add 两次，导致 FrameSet 静态构造抛异常 →
        //  整个类型初始化失败 → 客户端所有 PlayerObject/MonsterObject/NPCObject 构造全崩（满屏 NRE）。
        //  帧表是纯配置数据，重复项应以「后者覆盖前者」处理，不该炸掉客户端。
        public new void Add(MirAction action, Frame frame)
        {
            this[action] = frame;
        }

        public static FrameSet Player;
        public static FrameSet HumUpPlayer;   //羽化登仙专用帧表（Data\HumUp\，布局与 Player 不同，见下方定义）
        public static FrameSet HumUpCommon;   //羽化·战法道通用（UpC* 素材）帧表：站立[4+4]、攻击块 448 起、女半 1112
        public static FrameSet HumUpAssassin; //羽化登仙·刺客专用帧表（UpAss* 库布局与 UpWar* 不同，见下方定义）
        public static FrameSet HumUpTaoist;   //羽化登仙·道士专用帧表（UpTao* 库布局见下方定义）
        public static FrameSet HumUpArcher;   //羽化登仙·弓箭手专用帧表（UpArc* 库布局见下方定义）
        public static FrameSet HumUpWizard;   //羽化登仙·法师专用帧表（UpWiz* 库布局见下方定义）
        public static FrameSet DefaultNPC, DefaultMonster;
        public static List<FrameSet> DragonStatue, GreatFoxSpirit, HellBomb, CaveStatue, Gates;

        static FrameSet()
        {
            FrameSet frame;

            Player = new FrameSet();

            //Default NPC
            DefaultNPC = new FrameSet
            {
                { MirAction.站立动作, new Frame(0, 4, 0, 500) },
                { MirAction.挖矿展示, new Frame(12, 10, 0, 200) }
            };

            //Default Monster
            DefaultMonster = new FrameSet
            {
                { MirAction.站立动作, new Frame(0, 4, 0, 500) },
                { MirAction.行走动作, new Frame(32, 6, 0, 200) },
                { MirAction.近距攻击1, new Frame(80, 6, 0, 200) },
                { MirAction.被击动作, new Frame(128, 2, 0, 200) },
                { MirAction.死亡动作, new Frame(144, 10, 0, 200) },
                { MirAction.死后尸体, new Frame(153, 1, 9, 2000) },
                { MirAction.复活动作, new Frame(144, 10, 0, 200) { Reverse = true } }
            };

            #region DragonStatue
            //DragonStatue 1
            DragonStatue = new List<FrameSet> { (frame = new FrameSet()) };
            frame.Add(MirAction.站立动作, new Frame(301, 1, -1, 500));
            frame.Add(MirAction.远程攻击1, new Frame(301, 1, -1, 200));
            frame.Add(MirAction.被击动作, new Frame(302, 1, -1, 200));
            frame.Add(MirAction.死后尸体, new Frame(300, 1, -1, 2000));

            //DragonStatue 2
            DragonStatue.Add(frame = new FrameSet());
            frame.Add(MirAction.站立动作, new Frame(301, 1, -1, 500));
            frame.Add(MirAction.远程攻击1, new Frame(301, 1, -1, 200));
            frame.Add(MirAction.被击动作, new Frame(302, 1, -1, 200));
            frame.Add(MirAction.死后尸体, new Frame(300, 1, -1, 2000));
            
            //DragonStatue 3
            DragonStatue.Add(frame = new FrameSet());
            frame.Add(MirAction.站立动作, new Frame(301, 1, -1, 500));
            frame.Add(MirAction.远程攻击1, new Frame(301, 1, -1, 200));
            frame.Add(MirAction.被击动作, new Frame(302, 1, -1, 200));
            frame.Add(MirAction.死后尸体, new Frame(300, 1, -1, 2000));

            //DragonStatue 4
            DragonStatue.Add(frame = new FrameSet());
            frame.Add(MirAction.站立动作, new Frame(322, 1, -1, 500));
            frame.Add(MirAction.远程攻击1, new Frame(322, 1, -1, 200));
            frame.Add(MirAction.被击动作, new Frame(320, 1, -1, 200));
            frame.Add(MirAction.死后尸体, new Frame(321, 1, -1, 2000));

            //DragonStatue 5
            DragonStatue.Add(frame = new FrameSet());
            frame.Add(MirAction.站立动作, new Frame(322, 1, -1, 500));
            frame.Add(MirAction.远程攻击1, new Frame(322, 1, -1, 200));
            frame.Add(MirAction.被击动作, new Frame(320, 1, -1, 200));
            frame.Add(MirAction.死后尸体, new Frame(321, 1, -1, 2000));

            //DragonStatue 6
            DragonStatue.Add(frame = new FrameSet());
            frame.Add(MirAction.站立动作, new Frame(322, 1, -1, 500));
            frame.Add(MirAction.远程攻击1, new Frame(322, 1, -1, 200));
            frame.Add(MirAction.被击动作, new Frame(320, 1, -1, 200));
            frame.Add(MirAction.死后尸体, new Frame(321, 1, -1, 2000));
            #endregion

            #region GreatFoxSpirit
            //GreatFoxSpirit level 0
            GreatFoxSpirit = new List<FrameSet> { (frame = new FrameSet()) };
            frame.Add(MirAction.站立动作, new Frame(0, 20, -20, 100));
            frame.Add(MirAction.近距攻击1, new Frame(22, 8, -8, 120));
            frame.Add(MirAction.被击动作, new Frame(20, 2, -2, 200));
            frame.Add(MirAction.死亡动作, new Frame(300, 18, -18, 120));
            frame.Add(MirAction.死后尸体, new Frame(317, 1, -1, 1000));
            frame.Add(MirAction.复活动作, new Frame(300, 18, -18, 150) { Reverse = true });

            //GreatFoxSpirit level 1
            GreatFoxSpirit.Add(frame = new FrameSet());
            frame.Add(MirAction.站立动作, new Frame(60, 20, -20, 100));
            frame.Add(MirAction.近距攻击1, new Frame(82, 8, -8, 120));
            frame.Add(MirAction.被击动作, new Frame(80, 2, -2, 200));
            frame.Add(MirAction.死亡动作, new Frame(300, 18, -18, 120));
            frame.Add(MirAction.死后尸体, new Frame(317, 1, -1, 1000));
            frame.Add(MirAction.复活动作, new Frame(300, 18, -18, 150) { Reverse = true });

            //GreatFoxSpirit level 2
            GreatFoxSpirit.Add(frame = new FrameSet());
            frame.Add(MirAction.站立动作, new Frame(120, 20, -20, 100));
            frame.Add(MirAction.近距攻击1, new Frame(142, 8, -8, 120));
            frame.Add(MirAction.被击动作, new Frame(140, 2, -2, 200));
            frame.Add(MirAction.死亡动作, new Frame(300, 18, -18, 120));
            frame.Add(MirAction.死后尸体, new Frame(317, 1, -1, 1000));
            frame.Add(MirAction.复活动作, new Frame(300, 18, -18, 150) { Reverse = true });

            //GreatFoxSpirit level 3
            GreatFoxSpirit.Add(frame = new FrameSet());
            frame.Add(MirAction.站立动作, new Frame(180, 20, -20, 100));
            frame.Add(MirAction.近距攻击1, new Frame(202, 8, -8, 120));
            frame.Add(MirAction.被击动作, new Frame(200, 2, -2, 200));
            frame.Add(MirAction.死亡动作, new Frame(300, 18, -18, 120));
            frame.Add(MirAction.死后尸体, new Frame(317, 1, -1, 1000));
            frame.Add(MirAction.复活动作, new Frame(300, 18, -18, 150) { Reverse = true });

            //GreatFoxSpirit level 4
            GreatFoxSpirit.Add(frame = new FrameSet());
            frame.Add(MirAction.站立动作, new Frame(240, 20, -20, 100));
            frame.Add(MirAction.近距攻击1, new Frame(262, 8, -8, 120));
            frame.Add(MirAction.被击动作, new Frame(260, 2, -2, 200));
            frame.Add(MirAction.死亡动作, new Frame(300, 18, -18, 120));
            frame.Add(MirAction.死后尸体, new Frame(317, 1, -1, 1000));
            frame.Add(MirAction.复活动作, new Frame(300, 18, -18, 150) { Reverse = true });
            #endregion

            #region HellBombs
            //HellBomb1
            HellBomb = new List<FrameSet> { (frame = new FrameSet()) };
            frame.Add(MirAction.站立动作, new Frame(52, 9, -9, 100) { Blend = true });
            frame.Add(MirAction.近距攻击1, new Frame(999, 1, -1, 120) { Blend = true });
            frame.Add(MirAction.被击动作, new Frame(52, 9, -9, 100) { Blend = true });

            //HellBomb2
            HellBomb.Add(frame = new FrameSet());
            frame.Add(MirAction.站立动作, new Frame(70, 9, -9, 100) { Blend = true });
            frame.Add(MirAction.近距攻击1, new Frame(999, 1, -1, 120) { Blend = true });
            frame.Add(MirAction.被击动作, new Frame(70, 9, -9, 100) { Blend = true });

            //HellBomb3
            HellBomb.Add(frame = new FrameSet());
            frame.Add(MirAction.站立动作, new Frame(88, 9, -9, 100) { Blend = true });
            frame.Add(MirAction.近距攻击1, new Frame(999, 1, -1, 120) { Blend = true });
            frame.Add(MirAction.被击动作, new Frame(88, 9, -9, 100) { Blend = true });
            #endregion

            #region CaveStatues
            //CaveStatue1
            CaveStatue = new List<FrameSet> { (frame = new FrameSet()) };
            frame.Add(MirAction.站立动作, new Frame(0, 1, -1, 100) { Blend = false });
            frame.Add(MirAction.被击动作, new Frame(0, 1, -1, 100) { Blend = false });
            frame.Add(MirAction.死亡动作, new Frame(2, 8, -8, 100) { Blend = false });
            frame.Add(MirAction.死后尸体, new Frame(9, 1, -1, 100) { Blend = false });

            //CaveStatue2
            CaveStatue.Add(frame = new FrameSet());
            frame.Add(MirAction.站立动作, new Frame(18, 1, -1, 100) { Blend = false });
            frame.Add(MirAction.被击动作, new Frame(18, 1, -1, 100) { Blend = false });
            frame.Add(MirAction.死亡动作, new Frame(20, 8, -8, 100) { Blend = false });
            frame.Add(MirAction.死后尸体, new Frame(27, 1, -1, 100) { Blend = false });
            #endregion

            #region Gates
            //SabukGate
            Gates = new List<FrameSet> { (frame = new FrameSet()) };
            frame.Add(MirAction.站立动作, new Frame(0, 1, 7, 500) { Blend = false });
            frame.Add(MirAction.被击动作, new Frame(1, 3, 5, 200) { Blend = false });
            frame.Add(MirAction.死亡动作, new Frame(24, 10, -10, 200) { Blend = false });
            frame.Add(MirAction.死后尸体, new Frame(33, 1, -1, 2000) { Blend = false });
            frame.Add(MirAction.近距攻击1, new Frame(56, 6, -6, 200) { Blend = false });
            frame.Add(MirAction.近距攻击2, new Frame(64, 6, -6, 200) { Blend = false });
            #endregion

            #region Player
            //Common
            Player.Add(MirAction.站立动作, new Frame(0, 4, 0, 500, 0, 8, 0, 250));
            Player.Add(MirAction.行走动作, new Frame(32, 6, 0, 100, 64, 6, 0, 100));
            Player.Add(MirAction.跑步动作, new Frame(80, 6, 0, 100, 112, 6, 0, 100));
            Player.Add(MirAction.站立姿势, new Frame(128, 1, 0, 1000, 160, 1, 0, 1000));
            Player.Add(MirAction.站立姿势2, new Frame(300, 1, 5, 1000, 332, 1, 5, 1000));
            Player.Add(MirAction.近距攻击1, new Frame(136, 6, 0, 100, 168, 6, 0, 100));
            Player.Add(MirAction.近距攻击2, new Frame(184, 6, 0, 100, 216, 6, 0, 100));
            Player.Add(MirAction.近距攻击3, new Frame(232, 8, 0, 100, 264, 8, 0, 100));
            Player.Add(MirAction.近距攻击4, new Frame(416, 6, 0, 100, 448, 6, 0, 100));
            Player.Add(MirAction.施法动作, new Frame(296, 6, 0, 100, 328, 6, 0, 100));
            Player.Add(MirAction.挖矿展示, new Frame(344, 2, 0, 300, 376, 2, 0, 300));
            Player.Add(MirAction.被击动作, new Frame(360, 3, 0, 100, 392, 3, 0, 100));
            Player.Add(MirAction.死亡动作, new Frame(384, 4, 0, 100, 416, 4, 0, 100));
            Player.Add(MirAction.死后尸体, new Frame(387, 1, 3, 1000, 419, 1, 3, 1000));
            Player.Add(MirAction.复活动作, new Frame(384, 4, 0, 100, 416, 4, 0, 100) { Reverse = true });
            Player.Add(MirAction.挖矿动作, new Frame(184, 6, 0, 100, 216, 6, 0, 100));
            Player.Add(MirAction.刺客步刺, new Frame(139, 1, 5, 1000, 300, 1, 5, 1000));

            //刺客
            Player.Add(MirAction.刺客潜行, new Frame(464, 6, 0, 100, 496, 6, 0, 100));
            Player.Add(MirAction.刺客冲击, new Frame(80, 3, 3, 100, 112, 3, 3, 100));

            //弓箭
            Player.Add(MirAction.弓箭行走, new Frame(0, 6, 0, 100, 0, 6, 0, 100));
            Player.Add(MirAction.弓箭奔跑, new Frame(48, 6, 0, 100, 48, 6, 0, 100));
            Player.Add(MirAction.远程攻击1, new Frame(96, 8, 0, 100, 96, 8, 0, 100));
            Player.Add(MirAction.远程攻击2, new Frame(160, 8, 0, 100, 160, 8, 0, 100));
            Player.Add(MirAction.远程攻击3, new Frame(224, 8, 0, 100, 224, 8, 0, 100));
            Player.Add(MirAction.弓箭跳跃, new Frame(288, 8, 0, 100, 288, 8, 0, 100));

            //骑马动作
            Player.Add(MirAction.坐骑站立, new Frame(416, 4, 0, 500, 448, 4, 0, 500));
            Player.Add(MirAction.坐骑行走, new Frame(448, 8, 0, 100, 480, 8, 0, 500));
            Player.Add(MirAction.坐骑奔跑, new Frame(512, 6, 0, 100, 544, 6, 0, 100));
            Player.Add(MirAction.坐骑被击, new Frame(560, 3, 0, 100, 592, 3, 0, 100));
            Player.Add(MirAction.坐骑攻击, new Frame(584, 6, 0, 100, 616, 6, 0, 100));

            //钓鱼动作
            Player.Add(MirAction.钓鱼抛竿, new Frame(632, 8, 0, 100));
            Player.Add(MirAction.钓鱼等待, new Frame(696, 6, 0, 120));
            Player.Add(MirAction.钓鱼收线, new Frame(744, 8, 0, 100));

            #endregion

            #region HumUpPlayer（羽化登仙专用帧表）
            //★2026-11-09 逆向 E:\...\ClientResources\Data\HumUp\UpWarArmour\00.Lib 实测得出。
            //  HumUp 库布局与 CArmour(1616帧) 完全不同，不能复用 FrameSet.Player：
            //    · 男女半库各 784 帧（女 = 男 + 784），对应 PlayerObject 里 ArmourOffSet = 0 / 784
            //    · 每方向占 8 个槽位：常规动作用前 6 帧（Skip=2），被击用前 3 帧（Skip=5）
            //  已目检确认：站立 0、行走 64、跑步 128、被击 192(8×3)。
            //  死亡段在 256-341 连续区（倒地→翻身→撑起），精确切分待游戏内校准，先按 8×4 试。
            //  攻击段取 480（横挥，w 极差 28）、施法取 432（y 极差 61 大挥臂）——同为目检+数据推定。
            HumUpPlayer = new FrameSet
            {
                { MirAction.站立动作, new Frame(0, 6, 2, 250) },
                { MirAction.站立姿势, new Frame(0, 6, 2, 1000) },
                { MirAction.站立姿势2, new Frame(336, 6, 2, 1000) },
                { MirAction.行走动作, new Frame(64, 6, 2, 100) },
                { MirAction.跑步动作, new Frame(128, 6, 2, 100) },
                //★攻击块块首（每块 8 方向 × 8 帧）：400=双手下劈 / 464=突刺(普攻) /
                //  528=跳跃劈砍(8真帧) / 592=抡挥横扫 / 656=上撩 / 720=侧翻滚突进(8真帧)。
                //  Start 必须取块首，否则方向整体错位（432=blk400 的 dir4、480=blk464 的 dir2）。
                { MirAction.近距攻击1, new Frame(464, 6, 2, 100) },
                { MirAction.近距攻击2, new Frame(592, 6, 2, 100) },
                { MirAction.近距攻击3, new Frame(400, 6, 2, 100) },
                { MirAction.近距攻击4, new Frame(656, 6, 2, 100) },
                { MirAction.施法动作, new Frame(400, 6, 2, 100) },
                { MirAction.被击动作, new Frame(192, 3, 5, 100) },
                { MirAction.死亡动作, new Frame(256, 4, 4, 100) },
                { MirAction.死后尸体, new Frame(259, 1, 7, 1000) },
                { MirAction.复活动作, new Frame(256, 4, 4, 100) { Reverse = true } },
                { MirAction.挖矿展示, new Frame(0, 6, 2, 300) },
                { MirAction.挖矿动作, new Frame(592, 6, 2, 100) },
                { MirAction.刺客步刺, new Frame(464, 6, 2, 100) },
                //骑马/钓鱼/弓箭暂映射到常规动作凑合用（羽化状态骑马显示为步行，后续再补专用段）
                //★11-11 坐骑改造：马腿动画由 DrawMount 羽化分支按时间独立步进（UpMount 与身体库同布局），
                //  身体保持站姿即可 —— 奔跑不再映射奔跑段（人在马上迈腿会穿模），改站姿 + 快节奏微晃
                //★11-11 骑乘真姿势：骑乘时身体切 UpCArmour（通用库 792~1111 骑乘专属区块，
                //  站792(4帧)/走856(8帧)/跑920(6帧)/被击984(3帧)/攻1048(6帧)，女半 +1112；
                //  帧数与 UpMount 坐骑库逐段一一对应，马腿由 DrawMount 按身体 FrameIndex 同步驱动）
                { MirAction.坐骑站立, new Frame(792, 4, 4, 500) },
                { MirAction.坐骑行走, new Frame(856, 8, 0, 100) },
                { MirAction.坐骑奔跑, new Frame(920, 6, 2, 100) },
                { MirAction.坐骑被击, new Frame(984, 3, 5, 100) },
                { MirAction.坐骑攻击, new Frame(1048, 6, 2, 100) },
                { MirAction.弓箭行走, new Frame(0, 6, 2, 100) },
                { MirAction.弓箭奔跑, new Frame(128, 6, 2, 100) },
                { MirAction.远程攻击1, new Frame(464, 6, 2, 100) },
                { MirAction.远程攻击2, new Frame(592, 6, 2, 100) },
                { MirAction.远程攻击3, new Frame(656, 6, 2, 100) },
                { MirAction.弓箭跳跃, new Frame(128, 6, 2, 100) },
                //★补全：Frames.TryGetValue 失败会留下 null Frame 直接崩绘制，
                //  humup 库没有的动作用近似段顶上（羽化状态少这几个专属动作可接受）。
                //  注意：站立姿势2 已在上方绑定 336 段，此处不可再 Add（重复 key 会让
                //  FrameSet 静态构造抛 ArgumentException，整个类型不可用 → 全对象构造失败）。
                { MirAction.刺客潜行, new Frame(64, 6, 2, 100) },
                { MirAction.刺客冲击, new Frame(128, 6, 2, 100) },
                { MirAction.钓鱼抛竿, new Frame(592, 6, 2, 100) },
                { MirAction.钓鱼等待, new Frame(0, 6, 2, 300) },
                { MirAction.钓鱼收线, new Frame(464, 6, 2, 100) },
            };
            #endregion

            #region HumUpCommon（羽化·战法道通用 UpC* 帧表）
            //★2026-11-10 逆向 UpCArmour\01.Lib（2224 帧，男 0-1111 / 女 1112-2223）帧空缺 pattern 得出。
            //  与职业库差异：站立是 [4+4]（4 真帧），行走/跑步 [6+2]，被击 [3+5]，攻击块 448 起。
            //  UpCWeapon 段位与 UpCArmour 一致（武器库 1200 帧，尾部姿势段缺省=空帧不画，安全）。
            //  混搭（职业铠甲+通用武器等）也统一走本表：职业库在 [4+4] 段会少画 2 帧（动画短一点），
            //  不会读到空帧闪烁；反向（职业 6 帧表驱动通用库）才会读到空帧。
            HumUpCommon = new FrameSet
            {
                { MirAction.站立动作, new Frame(0, 4, 4, 250) },
                { MirAction.站立姿势, new Frame(0, 4, 4, 1000) },
                { MirAction.站立姿势2, new Frame(544, 4, 4, 1000) },
                { MirAction.行走动作, new Frame(64, 6, 2, 100) },
                { MirAction.跑步动作, new Frame(128, 6, 2, 100) },
                { MirAction.近距攻击1, new Frame(448, 6, 2, 100) },
                { MirAction.近距攻击2, new Frame(592, 4, 4, 100) },
                { MirAction.近距攻击3, new Frame(640, 6, 2, 100) },
                { MirAction.近距攻击4, new Frame(896, 6, 2, 100) },
                { MirAction.施法动作, new Frame(896, 6, 2, 100) },
                { MirAction.被击动作, new Frame(192, 3, 5, 100) },
                { MirAction.死亡动作, new Frame(256, 4, 4, 100) },
                { MirAction.死后尸体, new Frame(259, 1, 7, 1000) },
                { MirAction.复活动作, new Frame(256, 4, 4, 100) { Reverse = true } },
                { MirAction.挖矿展示, new Frame(0, 4, 4, 300) },
                { MirAction.挖矿动作, new Frame(448, 6, 2, 100) },
                { MirAction.刺客步刺, new Frame(448, 6, 2, 100) },
                //骑马/钓鱼/弓箭暂映射到近似段凑合用（通用库没有专属段）
                //★11-11：马腿动画由 DrawMount 独立驱动，坐骑奔跑改站姿（通用库 4 帧/方向）
                //★11-11 骑乘真姿势（同上：UpCArmour 骑乘区块，女半 +1112）
                { MirAction.坐骑站立, new Frame(792, 4, 4, 500) },
                { MirAction.坐骑行走, new Frame(856, 8, 0, 100) },
                { MirAction.坐骑奔跑, new Frame(920, 6, 2, 100) },
                { MirAction.坐骑被击, new Frame(984, 3, 5, 100) },
                { MirAction.坐骑攻击, new Frame(1048, 6, 2, 100) },
                { MirAction.弓箭行走, new Frame(0, 6, 2, 100) },
                { MirAction.弓箭奔跑, new Frame(128, 6, 2, 100) },
                { MirAction.远程攻击1, new Frame(448, 6, 2, 100) },
                { MirAction.远程攻击2, new Frame(592, 4, 4, 100) },
                { MirAction.远程攻击3, new Frame(640, 6, 2, 100) },
                { MirAction.弓箭跳跃, new Frame(128, 6, 2, 100) },
                { MirAction.刺客潜行, new Frame(64, 6, 2, 100) },
                { MirAction.刺客冲击, new Frame(128, 6, 2, 100) },
                //★11-12 钓鱼真姿势：持竿时身体已切通用库（UpCArmour，见 SetHumUpLibraries），
                //  钓鱼三动作绑通用库实测段（BFS 逐帧贴合验证，竿段 0/64/128 与身体段 Δ=600 恒定配对）：
                //  抛竿 600（8方向×8帧全有效）/ 等待 664（仅前4方向，各6帧；后4方向 DrawBody 特判画站立帧）/
                //  收线 728（8方向×8帧全有效）。原版 CArmour 是 632/696/744，羽化库整体前移 32 帧，不能照搬。
                { MirAction.钓鱼抛竿, new Frame(600, 8, 0, 100) },
                { MirAction.钓鱼等待, new Frame(664, 6, 2, 120) },
                { MirAction.钓鱼收线, new Frame(728, 8, 0, 100) },
            };
            #endregion

            #region HumUpAssassin（羽化登仙·刺客专用帧表）
            //★2026-11-09 逆向 UpAssArmour\00.Lib 实测得出。刺客库 1824 帧（男女各 912），布局与战士库（各 784）不同：
            //    · 站立 0、行走 64、跑步 128（与战士一致）
            //    · 128-191 = [6+2]×8 步幅中等的快走（用户反馈"跑步像快走"后目检确认，不是跑步！）
            //    · 192-255 = [6+2]×8 身体前倾大跨步冲刺 → 真正的跑步（原误判为潜行）
            //    · 256-319 = [3+5]×8 → 被击（3 帧）
            //    · 320-399 = 连续区，目检为倒地→躺地 → 死亡(320-359, 每方向5帧) + 复活(360-399)
            //    · 400-847 = 六个攻击/施法候选段，正面序列目检判读（用户反馈修正后定稿）：
            //        400 = 双手合于胸前蓄力姿态（曾被误判为普攻）→ 站立姿势2（战斗待机）
            //        464 = 旋身扫击 → 近距攻击3
            //        528 = 高踢腿 → 近距攻击4
            //        592 = 双臂交叉于胸→向前突刺（刺客标志性普攻）→ 近距攻击1
            //        720 = 单臂挥砍 → 近距攻击2
            //        784 = 双臂大张开→收拢聚气 → 施法动作
            //    · 656-725 = 连续区，目检为腾空飞踢 → 刺客冲击
            //    · 848-911 = 连续区，腾空/翻滚被击飞系（特殊受击，暂不接）
            //    · 女半库偏移 +912（对应 PlayerObject 里 ArmourOffSet）
            HumUpAssassin = new FrameSet
            {
                { MirAction.站立动作, new Frame(0, 6, 2, 250) },
                { MirAction.站立姿势, new Frame(0, 6, 2, 1000) },
                { MirAction.站立姿势2, new Frame(400, 6, 2, 1000) },
                { MirAction.行走动作, new Frame(64, 6, 2, 100) },
                { MirAction.跑步动作, new Frame(192, 6, 2, 100) },
                { MirAction.刺客潜行, new Frame(128, 6, 2, 100) },
                { MirAction.近距攻击1, new Frame(592, 6, 2, 100) },
                { MirAction.近距攻击2, new Frame(720, 6, 2, 100) },
                { MirAction.近距攻击3, new Frame(464, 6, 2, 100) },
                { MirAction.近距攻击4, new Frame(528, 6, 2, 100) },
                { MirAction.刺客步刺, new Frame(592, 6, 2, 100) },
                { MirAction.刺客冲击, new Frame(656, 6, 2, 100) },
                { MirAction.施法动作, new Frame(784, 6, 2, 100) },
                { MirAction.被击动作, new Frame(256, 3, 5, 100) },
                { MirAction.死亡动作, new Frame(320, 5, 3, 100) },
                { MirAction.死后尸体, new Frame(324, 1, 3, 2000) },
                { MirAction.复活动作, new Frame(360, 5, 3, 100) { Reverse = true } },
                { MirAction.挖矿展示, new Frame(0, 6, 2, 300) },
                { MirAction.挖矿动作, new Frame(720, 6, 2, 100) },
                //骑马/弓箭/钓鱼映射到近似段（同战士处理，羽化状态凑合用）
                //★11-11：坐骑奔跑改站姿（马腿动画由 DrawMount 独立驱动）
                //★11-11 骑乘真姿势（同上：UpCArmour 骑乘区块，女半 +1112）
                { MirAction.坐骑站立, new Frame(792, 4, 4, 500) },
                { MirAction.坐骑行走, new Frame(856, 8, 0, 100) },
                { MirAction.坐骑奔跑, new Frame(920, 6, 2, 100) },
                { MirAction.坐骑被击, new Frame(984, 3, 5, 100) },
                { MirAction.坐骑攻击, new Frame(1048, 6, 2, 100) },
                { MirAction.弓箭行走, new Frame(0, 6, 2, 100) },
                { MirAction.弓箭奔跑, new Frame(192, 6, 2, 100) },
                { MirAction.远程攻击1, new Frame(592, 6, 2, 100) },
                { MirAction.远程攻击2, new Frame(720, 6, 2, 100) },
                { MirAction.远程攻击3, new Frame(464, 6, 2, 100) },
                { MirAction.弓箭跳跃, new Frame(656, 6, 2, 100) },
                { MirAction.钓鱼抛竿, new Frame(784, 6, 2, 100) },
                { MirAction.钓鱼等待, new Frame(0, 6, 2, 300) },
                { MirAction.钓鱼收线, new Frame(720, 6, 2, 100) },
            };
            #endregion

            #region HumUpTaoist（羽化登仙·道士专用帧表）
            //★2026-11-09 逆向 UpTaoArmour\00.Lib 实测得出。道士库 1440 帧（男女各 720，女偏移 +720）：
            //    · 站立 0、行走 64、跑步 128（同前两库）
            //    · 192-255 = [3+5]×8 → 被击（3 帧，同战士位置）
            //    · 256-335 = 连续区 80 帧 → 死亡(256-295, 每方向5帧) + 复活(296-335)
            //    · 336-399 = 蹲身防御姿态 → 站立姿势2
            //    · 400-463 = 双手合拢→双臂向两侧完全张开 → 施法动作（正面序列目检）
            //    · 464-527 = 手抬至嘴边→向前甩出（掷符）→ 近距攻击1（道士标志性普攻）
            //    · 528-591 = 连续区 64 帧，扭身挥臂近战 → 挖矿动作（暂用）
            //    · 592-655 = 屈肘扭身近战连击 → 近距攻击2
            //    · 656-719 = 单臂高举过头 → 近距攻击3（大施法系技能）
            HumUpTaoist = new FrameSet
            {
                { MirAction.站立动作, new Frame(0, 6, 2, 250) },
                { MirAction.站立姿势, new Frame(0, 6, 2, 1000) },
                { MirAction.站立姿势2, new Frame(336, 6, 2, 1000) },
                { MirAction.行走动作, new Frame(64, 6, 2, 100) },
                { MirAction.跑步动作, new Frame(128, 6, 2, 100) },
                { MirAction.刺客潜行, new Frame(64, 6, 2, 100) },
                { MirAction.刺客冲击, new Frame(128, 6, 2, 100) },
                { MirAction.刺客步刺, new Frame(464, 6, 2, 100) },
                { MirAction.近距攻击1, new Frame(464, 6, 2, 100) },
                { MirAction.近距攻击2, new Frame(592, 6, 2, 100) },
                { MirAction.近距攻击3, new Frame(656, 6, 2, 100) },
                { MirAction.近距攻击4, new Frame(464, 6, 2, 100) },
                { MirAction.施法动作, new Frame(400, 6, 2, 100) },
                { MirAction.被击动作, new Frame(192, 3, 5, 100) },
                { MirAction.死亡动作, new Frame(256, 5, 3, 100) },
                { MirAction.死后尸体, new Frame(260, 1, 3, 2000) },
                { MirAction.复活动作, new Frame(296, 5, 3, 100) { Reverse = true } },
                { MirAction.挖矿展示, new Frame(0, 6, 2, 300) },
                { MirAction.挖矿动作, new Frame(592, 6, 2, 100) },
                //骑马/弓箭/钓鱼映射到近似段（羽化状态凑合用）
                //★11-11：坐骑奔跑改站姿
                //★11-11 骑乘真姿势：骑乘时身体切 UpCArmour（通用库 792~1111 骑乘专属区块，
                //  站792(4帧)/走856(8帧)/跑920(6帧)/被击984(3帧)/攻1048(6帧)，女半 +1112；
                //  帧数与 UpMount 坐骑库逐段一一对应，马腿由 DrawMount 按身体 FrameIndex 同步驱动）
                { MirAction.坐骑站立, new Frame(792, 4, 4, 500) },
                { MirAction.坐骑行走, new Frame(856, 8, 0, 100) },
                { MirAction.坐骑奔跑, new Frame(920, 6, 2, 100) },
                { MirAction.坐骑被击, new Frame(984, 3, 5, 100) },
                { MirAction.坐骑攻击, new Frame(1048, 6, 2, 100) },
                { MirAction.弓箭行走, new Frame(0, 6, 2, 100) },
                { MirAction.弓箭奔跑, new Frame(128, 6, 2, 100) },
                { MirAction.远程攻击1, new Frame(464, 6, 2, 100) },
                { MirAction.远程攻击2, new Frame(400, 6, 2, 100) },
                { MirAction.远程攻击3, new Frame(656, 6, 2, 100) },
                { MirAction.弓箭跳跃, new Frame(128, 6, 2, 100) },
                { MirAction.钓鱼抛竿, new Frame(400, 6, 2, 100) },
                { MirAction.钓鱼等待, new Frame(0, 6, 2, 300) },
                { MirAction.钓鱼收线, new Frame(464, 6, 2, 100) },
            };
            #endregion

            #region HumUpArcher（羽化登仙·弓箭手专用帧表）
            //★2026-11-09 逆向 UpArcArmour\00.Lib 实测得出。弓手库只有 768 帧（男女各 384，女偏移 +384），
            //  是五职业里最"瘦"的一套——韩服客户端从未实装弓手羽化，资源本身就是残缺的：
            //    · 每方向组 8 帧；0-127 段是 [6+2]（6 真帧+2 个 4x4 空槽），128 之后无空槽（8 帧全真）
            //    · ★2026-11-10 二次目检修正分段（渲染 dir0/dir4 逐帧比对）：
            //      块 0   0-63   = 站立呼吸 [6+2]×8（六帧全原位，脚不动、身体呼吸摆动；
            //                      此前按帧差 14.7 误判为行走——帧差大是羽化披风大幅飘动所致）
            //      块 1   64-127 = 行走 [6+2]×8（目检确认为迈步；库里没有独立跑步段，跑步共用本段）
            //      块 2   128-191 = 攻击段A（挥臂连击，8 帧密集）
            //      块 3   192-255 = 攻击段B（双手举过头下劈，8 帧密集）
            //      块 4   256-319 = 攻击段C（举臂/突刺，8 帧密集）
            //      块 5   320-383 = 弓箭跳跃（蹲→腾空→落地，8 帧密集，dir4 正面序列目检确认）
            //    · 128 之后无空槽 → Frame.OffSet = Count+Skip 必须保持 8，否则方向偏移错位。
            HumUpArcher = new FrameSet
            {
                //★站立（11-10 三改）：0-63 段经下身冻结重烘焙（袍摆/腿/阴影取首帧静止，上身保留晃动），
                //  不再有原地踏步感。速度按用户要求放慢：250 → 400（6 帧×400ms = 2.4 秒一个呼吸循环）。
                { MirAction.站立动作, new Frame(0, 6, 2, 400) },
                { MirAction.站立姿势, new Frame(0, 6, 2, 1000) },
                { MirAction.站立姿势2, new Frame(0, 6, 2, 1000) },
                { MirAction.行走动作, new Frame(64, 6, 2, 100) },
                { MirAction.跑步动作, new Frame(64, 6, 2, 100) },
                { MirAction.刺客潜行, new Frame(64, 6, 2, 100) },
                { MirAction.刺客冲击, new Frame(320, 8, 0, 80) },
                { MirAction.刺客步刺, new Frame(128, 8, 0, 85) },
                { MirAction.近距攻击1, new Frame(128, 8, 0, 85) },
                { MirAction.近距攻击2, new Frame(192, 8, 0, 85) },
                { MirAction.近距攻击3, new Frame(256, 8, 0, 85) },
                { MirAction.近距攻击4, new Frame(128, 8, 0, 85) },
                { MirAction.施法动作, new Frame(256, 8, 0, 85) },
                { MirAction.远程攻击1, new Frame(128, 8, 0, 85) },
                { MirAction.远程攻击2, new Frame(192, 8, 0, 85) },
                { MirAction.远程攻击3, new Frame(256, 8, 0, 85) },
                { MirAction.弓箭跳跃, new Frame(320, 8, 0, 80) },
                { MirAction.被击动作, new Frame(128, 1, 7, 200) },
                { MirAction.死亡动作, new Frame(320, 1, 7, 200) },
                { MirAction.死后尸体, new Frame(320, 1, 7, 2000) },
                { MirAction.复活动作, new Frame(320, 1, 7, 200) },
                { MirAction.挖矿展示, new Frame(128, 1, 7, 300) },
                { MirAction.挖矿动作, new Frame(192, 8, 0, 100) },
                //★11-11：弓库布局特殊（站立 128 段单帧），骑乘行走/奔跑统一站姿（马腿独立驱动）
                //★11-11 骑乘真姿势（同上：UpCArmour 骑乘区块，女半 +1112）
                { MirAction.坐骑站立, new Frame(792, 4, 4, 500) },
                { MirAction.坐骑行走, new Frame(856, 8, 0, 100) },
                { MirAction.坐骑奔跑, new Frame(920, 6, 2, 100) },
                { MirAction.坐骑被击, new Frame(984, 3, 5, 100) },
                { MirAction.坐骑攻击, new Frame(1048, 6, 2, 100) },
                { MirAction.弓箭行走, new Frame(64, 6, 2, 100) },
                { MirAction.弓箭奔跑, new Frame(64, 6, 2, 100) },
                { MirAction.钓鱼抛竿, new Frame(256, 8, 0, 85) },
                { MirAction.钓鱼等待, new Frame(128, 1, 7, 300) },
                { MirAction.钓鱼收线, new Frame(192, 8, 0, 85) },
            };
            #endregion

            #region HumUpWizard（羽化登仙·法师专用帧表）
            //★2026-11-09 逆向 UpWizArmour\00.Lib 实测得出。法师库 1440 帧（男女各 720，女偏移 +720）：
            //    · 站立 0 / 行走 64 / 跑步 128 各 [6+2]×8（帧差标定 8.8/16.4/21.6，与战士同构）
            //    · 192-255 = [3+5]×8 → 被击（3 帧，同战士/道士位置）
            //    · 256-335 = 死亡/复活杂烩区（后仰倒→扑地→跪爬→蹲起，dir0 目检）；
            //      320-335 = 趴地→蹲立起身过程 → 复活动作
            //    · ★336 起组首对齐 8（336-343 组：336-341 真帧+342,343 空），六大动作块（块首 336+64n）：
            //      336 = 双臂前伸推掌（火球施法；dir0 背视手臂被身体遮挡近似站立，正视明显）
            //      400 = 大挥臂下劈；464 = 扭身横挥（普攻挥杖）；528 = 双臂举过头下压（全方向可见，
            //            最有施法观感）；592 = 屈肘挥击；656 = 单臂上举前指（施法变体）
            HumUpWizard = new FrameSet
            {
                { MirAction.站立动作, new Frame(0, 6, 2, 250) },
                { MirAction.站立姿势, new Frame(0, 6, 2, 1000) },
                { MirAction.站立姿势2, new Frame(0, 6, 2, 1000) },
                { MirAction.行走动作, new Frame(64, 6, 2, 100) },
                { MirAction.跑步动作, new Frame(128, 6, 2, 100) },
                { MirAction.刺客潜行, new Frame(64, 6, 2, 100) },
                { MirAction.刺客冲击, new Frame(128, 6, 2, 100) },
                { MirAction.刺客步刺, new Frame(464, 6, 2, 100) },
                { MirAction.近距攻击1, new Frame(464, 6, 2, 100) },
                { MirAction.近距攻击2, new Frame(592, 6, 2, 100) },
                { MirAction.近距攻击3, new Frame(400, 6, 2, 100) },
                { MirAction.近距攻击4, new Frame(528, 6, 2, 100) },
                //施法用 528（双臂高举，八个方向都看得见动作）；推掌 336 背视近似站立，作远程攻击1
                { MirAction.施法动作, new Frame(528, 6, 2, 100) },
                { MirAction.远程攻击1, new Frame(336, 6, 2, 100) },
                { MirAction.远程攻击2, new Frame(400, 6, 2, 100) },
                { MirAction.远程攻击3, new Frame(656, 6, 2, 100) },
                { MirAction.弓箭跳跃, new Frame(128, 6, 2, 100) },
                { MirAction.被击动作, new Frame(192, 3, 5, 100) },
                { MirAction.死亡动作, new Frame(256, 4, 4, 100) },
                { MirAction.死后尸体, new Frame(259, 1, 7, 2000) },
                { MirAction.复活动作, new Frame(320, 3, 5, 100) },
                { MirAction.挖矿展示, new Frame(0, 6, 2, 300) },
                { MirAction.挖矿动作, new Frame(464, 6, 2, 100) },
                //★11-11：坐骑奔跑改站姿
                //★11-11 骑乘真姿势：骑乘时身体切 UpCArmour（通用库 792~1111 骑乘专属区块，
                //  站792(4帧)/走856(8帧)/跑920(6帧)/被击984(3帧)/攻1048(6帧)，女半 +1112；
                //  帧数与 UpMount 坐骑库逐段一一对应，马腿由 DrawMount 按身体 FrameIndex 同步驱动）
                { MirAction.坐骑站立, new Frame(792, 4, 4, 500) },
                { MirAction.坐骑行走, new Frame(856, 8, 0, 100) },
                { MirAction.坐骑奔跑, new Frame(920, 6, 2, 100) },
                { MirAction.坐骑被击, new Frame(984, 3, 5, 100) },
                { MirAction.坐骑攻击, new Frame(1048, 6, 2, 100) },
                { MirAction.弓箭行走, new Frame(0, 6, 2, 100) },
                { MirAction.弓箭奔跑, new Frame(128, 6, 2, 100) },
                { MirAction.钓鱼抛竿, new Frame(336, 6, 2, 100) },
                { MirAction.钓鱼等待, new Frame(0, 6, 2, 300) },
                { MirAction.钓鱼收线, new Frame(464, 6, 2, 100) },
            };
            #endregion
        }
    }

    public class Frame
    {
        public int Start, Count, Skip, EffectStart, EffectCount, EffectSkip;
        public int Interval, EffectInterval;
        public bool Reverse, Blend;

        public int OffSet
        {
            get { return Count + Skip; }
        }

        public int EffectOffSet
        {
            get { return EffectCount + EffectSkip; }
        }

        public Frame(int start, int count, int skip, int interval, int effectstart = 0, int effectcount = 0, int effectskip = 0, int effectinterval = 0)
        {
            Start = start;
            Count = count;
            Skip = skip;
            Interval = interval;
            EffectStart = effectstart;
            EffectCount = effectcount;
            EffectSkip = effectskip;
            EffectInterval = effectinterval;
        }

        public Frame(BinaryReader reader)
        {
            Start = reader.ReadInt32();
            Count = reader.ReadInt32();
            Skip = reader.ReadInt32();
            Interval = reader.ReadInt32();
            EffectStart = reader.ReadInt32();
            EffectCount = reader.ReadInt32();
            EffectSkip = reader.ReadInt32();
            EffectInterval = reader.ReadInt32();
            Reverse = reader.ReadBoolean();
            Blend = reader.ReadBoolean();
        }
    }

}
