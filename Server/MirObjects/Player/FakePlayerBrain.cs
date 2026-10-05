using System;
using System.Collections.Generic;
using System.Drawing;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects.Monsters;

namespace Server.MirObjects
{
    /// <summary>假人当前在干什么（主要用于 GM 命令 / 调试观察）</summary>
    public enum FakeBotState
    {
        Roam,      // 在野外巡游找怪
        Idle,      // 站着发呆
        Combat,    // 正在打怪 / 打架
        Loot,      // 跑去捡地上的东西
        Restock,   // 在城里补给（等回血回蓝 / 补药）
        Dead,      // 躺尸，等着自动复活
        Follow,    // 跟着组队的玩家跑（跟班模式）
    }

    /// <summary>
    /// 假人的「大脑」。
    ///
    /// 设计要点：
    ///   1. 它只是一个决策器，所有动作都走 PlayerObject 的真实接口（Walk/Run/Attack/Magic/PickUp/Chat…），
    ///      所以真人客户端看到的一切都是服务端原生表现，不需要改一行客户端代码；
    ///   2. Tick() 由 FakePlayerObject.Process() 每个主循环驱动一次，但又会被 Process 的
    ///      OperateTime 节流，所以这里所有重活（找怪、找物品）都用时间戳再卡一层，
    ///      保证 50 个假人加起来的开销可以忽略；
    ///   3. 所有节拍都带随机抖动，避免 50 个假人像机器人一样整齐划一（这恰恰是最容易露馅的地方）。
    /// </summary>
    public class FakePlayerBrain
    {
        protected static Envir Envir => Envir.Main;

        public readonly FakePlayerObject Player;

        // ---------------- 行为节拍（毫秒） ----------------
        private const int SearchDelay = 800;    // 找怪间隔
        private const int RoamDelay = 1100;     // 巡游换目标点间隔
        private const int PotDelay = 700;       // 检查要不要吃药
        private const int LootDelay = 700;      // 找地面物品间隔
        private const int ViewRange = 9;        // 找怪半径（切比雪夫距离，格）
        private const int LootRange = 6;        // 找地面物品半径
        private const int PotionLootRange = 6;  // 身上没药时，最多跑这么远去找地上的药水
        private const int HomeRange = 16;       // 离出生点超过这个距离就往回走
        private const int FollowRange = 4;      // 组队跟随时，离队长超过这么多格就追上去
        private const int FollowLureRange = 12; // 跟随时，最多打离队长这么远的怪（再远就不追了，免得把人丢了）
        private const int FollowCatchupRange = 13;  // 离队长超过这么多格 → 放下手上的活先追人（比上面大 1 格做迟滞）
        private const int FollowTeleportRange = 16; // 超过这么多格 → 直接传送到队长身边（路被堵住 / 队长飞了）
        private const int FollowAssistRange = 12;   // 在队长周围这么大范围内找「该帮着打谁」
        private const long DuelTime = 45000;    // 假人之间单挑最长打多久（打不完就各回各家）
        private const long AssistHoldTime = 12000;  // 帮队长打架时，瞄准了就先咬着不放这么久

        // ---------------- 运行状态 ----------------
        private FakeBotState _state = FakeBotState.Roam;
        public FakeBotState State => _state;

        private long _nextSearch, _nextRoam, _nextPot, _nextLoot, _nextChat, _nextMapHop, _nextBagCheck;
        private long _idleUntil, _restockUntil;

        // ---- 组队 / 跟随 ----
        private PlayerObject _leader;          // 当前跟的队长（组里的真人玩家）；null = 没组队
        private bool _wasGrouped;              // 上一拍的组队状态（用来只切一次 AMode）
        private long _nextFollowTp;            // 跨图跟随时两次传送之间的最小间隔
        private long _nextRefill;              // 跟随时在城里顺手补药的间隔
        private MapObject _assistTarget;       // 正在替队长打的那个人（帮 PK 用）
        private long _assistUntil;             // 这场架帮到什么时候（队长还在打就续期）

        // ---- 假人互殴 ----
        private long _nextPKCheck;             // 下次考虑「找个人切磋一下」的时间
        private long _duelUntil;               // 这场单挑的截止时间（到点各回各家）

        // HumanObject.SetOperateTime() 是 `OperateTime = Envir.Time`，也就是假人会被主循环
        // 以「每次循环」的频率驱动（一秒可能几十上百次）。真人客户端本来就 600ms 才动一步，
        // 所以这里再卡一层自己的节拍，既省 CPU，又不会让动作看起来变迟钝。
        private long _nextUpdate;

        private MapObject _target;
        private ItemObject _lootTarget;

        // 「身上快没药了」的急救捡药
        private long _nextMedCheck;            // 下次扫地面药水的节拍
        private ItemObject _medOnGround;       // 最近一次扫到的地面药水（AutoPot 用它决定「要不要回城」）

        private Point _roamTarget;
        private Point _homePoint;

        // 追怪时的卡位检测
        private int _lastTargetDist = int.MaxValue;
        private int _stuckTicks;
        private int _stuckRoam;

        // 「打不动就撤」/ 还手 / 躲避
        private MapObject _lastCombatTarget;   // 正在计时打谁
        private int _attackFails;              // 技能连续丢失（蓝不够 / 打不到）
        private int _lastTargetHP = -1;        // 上次检查时目标血量
        private long _nextHPCheck;             // 下次检查「这怪到底打不打得动」
        private long _dodgeUntil;              // 躲避期（血量过低时后撤走位）

        private Spell _attackSpell = Spell.None;   // 本职业的主力攻击技能（近战职业为 None）
        private Spell _healSpell = Spell.None;     // 治病（道士）
        private bool _thrusting;                   // 是否会刺杀（战士隔位）
        private bool _errorLogged;

        public FakePlayerBrain(FakePlayerObject player)
        {
            Player = player;

            _roamTarget = player.CurrentLocation;
            _homePoint = player.CurrentLocation;

            _nextChat = Envir.Time + Envir.Random.Next(60000, 300000);   // 1~5 分钟后开口
            _nextMapHop = Envir.Time + Envir.Random.Next(600000, 1800000); // 10~30 分钟后考虑换图

            PickSpells();
        }

        /// <summary>按职业挑技能：有高级的就用高级的，没有就用普攻</summary>
        private void PickSpells()
        {
            var cls = Player.Class;

            bool Has(Spell s) => Player.GetMagic(s) != null;

            switch (cls)
            {
                case MirClass.战士:
                    _thrusting = Has(Spell.Thrusting) && Player.Level >= 3; // 刺杀：直线 2 格
                    _attackSpell = Spell.None;
                    break;

                case MirClass.刺客:
                    _attackSpell = Has(Spell.DoubleSlash) ? Spell.DoubleSlash : Spell.None;
                    break;

                case MirClass.法师:
                    // 优先度：雷电 > 大火球 > 火球 > 冰咆哮
                    if (Has(Spell.ThunderBolt)) _attackSpell = Spell.ThunderBolt;
                    else if (Has(Spell.GreatFireBall)) _attackSpell = Spell.GreatFireBall;
                    else if (Has(Spell.FireBall)) _attackSpell = Spell.FireBall;
                    else if (Has(Spell.FrostCrunch)) _attackSpell = Spell.FrostCrunch;
                    else _attackSpell = Spell.None;
                    break;

                case MirClass.道士:
                    // 灵魂火符用来打；没学会就纯靠砍（绝对不能把治病当攻击手段，那会把怪治好）
                    _attackSpell = Has(Spell.SoulFireBall) ? Spell.SoulFireBall : Spell.None;

                    _healSpell = Has(Spell.Healing) ? Spell.Healing : Spell.None;
                    break;

                case MirClass.弓箭:
                    if (Has(Spell.StraightShot)) _attackSpell = Spell.StraightShot;
                    else _attackSpell = Spell.None;
                    break;
            }
        }

        /// <summary>主循环驱动入口。任何异常都不允许把服务端主循环带崩。</summary>
        public void Tick()
        {
            try
            {
                Update();
            }
            catch (Exception ex)
            {
                if (_errorLogged) return;

                _errorLogged = true;
                MessageQueue.Instance.Enqueue($"[假人AI] {Player?.Name} 出错：{ex}");
            }
        }

        // ==================================================================================
        //  主决策
        // ==================================================================================
        private void Update()
        {
            FakePlayerObject p = Player;

            if (p.Info == null || p.CurrentMap == null || p.Node == null) return;

            // ---- 0. 整体节拍：400~700ms 思考一次（带抖动，50 个假人不会同时醒） ----
            if (Envir.Time < _nextUpdate) return;
            _nextUpdate = Envir.Time + Envir.Random.Next(380, 700);

            // ---- 0.5 组队：有人邀请就答应，顺便算清楚「现在跟的是谁」 ----
            GroupUpdate();

            bool following = IsFollowing;

            // 跟着的那个真人玩家阵亡了 → 这一趟就算结束：假人自行下线，过一会儿再自己爬上来
            if (following && (_leader.Dead || _leader.Node == null))
            {
                _state = FakeBotState.Follow;
                FakePlayerManager.RequestRelogin(p);
                return;
            }

            // ---- 1. 死亡：躺一会儿自动回城复活 ----
            if (p.Dead)
            {
                _state = FakeBotState.Dead;
                _target = null;
                _dodgeUntil = 0;
                _duelUntil = 0;
                _lastCombatTarget = null;
                _attackFails = 0;

                if (Envir.Time >= p.NextReviveTime)
                {
                    p.TownRevive();

                    // 刚回城：先站着发发呆，顺便当「回来补给」处理
                    _state = FakeBotState.Restock;
                    _restockUntil = Envir.Time + Envir.Random.Next(8000, 25000);
                    _idleUntil = Envir.Time;
                    _roamTarget = p.CurrentLocation;
                    _target = null;
                }
                return;
            }

            // ---- 2. 发呆中：什么都不做，只维护吃药 ----
            if (Envir.Time < _idleUntil)
            {
                // 跟着队长的时候别发呆太久：人一走就得跟上
                if (following && Functions.MaxDistance(p.CurrentLocation, _leader.CurrentLocation) > FollowRange * 2)
                    _idleUntil = 0;
                else
                {
                    _state = FakeBotState.Idle;
                    AutoPot();
                    return;
                }
            }

            AutoPot();
            CleanBag();

            // ---- 2.2 跟班第一优先：队长跑远了（或换图了）→ 先把人追上 ----
            // 这条必须排在打架 / 捡东西前面：人都跟丢了，多打一只怪没有任何意义。
            if (following && NeedCatchup())
            {
                DropTarget();
                FollowUpdate();
                ChatUpdate();
                return;
            }

            // ---- 2.1 药快见底 → 先把地上的药捡了（急救通道） ----
            // 下面第 5 段的捡物只在「手上没目标」时才做，而野外几乎永远有怪，
            // 结果假人一路打怪、一路把地上的药水全错过。这里给药品单开一条通道：
            // 只要身上的血药或蓝药少于 2 瓶，就先绕过去把脚边的药捡起来。
            CountPotions(out int hpPots, out int mpPots);

            if (hpPots < 2 || mpPots < 2)
            {
                // 背包满了 → 立刻腾位置，不然地上的药捡不进来（CleanBag 平时十几秒才轮到一次）
                if (FreeBagSpace() == 0)
                {
                    _nextBagCheck = 0;
                    CleanBag();
                }

                bool empty = hpPots == 0 || mpPots == 0;

                if (Envir.Time >= _nextMedCheck)
                {
                    _nextMedCheck = Envir.Time + 1200 + Envir.Random.Next(0, 800);
                    _medOnGround = FindPotionOnGround(empty ? PotionLootRange : 3);
                }

                ItemObject med = _medOnGround;

                if (med != null && (med.Node == null || med.CurrentMap != p.CurrentMap)) med = null;

                if (med != null)
                {
                    _state = FakeBotState.Loot;

                    if (p.CurrentLocation == med.CurrentLocation)
                    {
                        p.PickUp();
                        _medOnGround = null;
                        _lootTarget = null;
                        _nextLoot = Envir.Time + 400;
                    }
                    else
                    {
                        StepTo(med.CurrentLocation);
                    }

                    ChatUpdate();
                    return;
                }
            }
            else
            {
                _medOnGround = null;
                _nextMedCheck = 0;
            }

            // ---- 2.5 躲避中：血量过低，先拉开距离再说（远程边撤边打） ----
            if (Envir.Time < _dodgeUntil)
            {
                DodgeUpdate();
                return;
            }

            // ---- 3. 补给状态：在城里站一会儿，把药补满，然后挑张图继续 ----
            //（跟着队长的时候不补给了 —— 半路跑去买药就把人跟丢了）
            if (_state == FakeBotState.Restock && !following)
            {
                RestockUpdate();
                return;
            }

            // ---- 4. 找目标（打架 / 打怪） ----
            if (Envir.Time >= _nextSearch)
            {
                _nextSearch = Envir.Time + SearchDelay + Envir.Random.Next(200, 600);

                // 正在跟玩家（真人 / 别的假人）过招时，别自动把目标换回野怪
                bool pvpFight = _target is HumanObject
                                && !_target.Dead && _target.Node != null
                                && _target.CurrentMap == p.CurrentMap;

                if (following)
                {
                    // ---- 4a. 帮队长打架（优先级最高）----
                    // 队长在打谁，我们就打谁；谁在打队长，我们也打谁。
                    if (!pvpFight)
                    {
                        MapObject assist = FindAssistTarget(_leader, FollowAssistRange);

                        if (assist != null && assist != _target)
                        {
                            _target = assist;
                            _assistTarget = assist;
                            _assistUntil = Envir.Time + AssistHoldTime;
                            _lastCombatTarget = null;
                            _duelUntil = 0;
                            StuckReset();
                        }

                        pvpFight = _target is HumanObject;
                    }

                    // ---- 4b. 没有架可打 → 打队长身边的怪 ----
                    if (!pvpFight && (_target == null || _target.Dead || _target.Node == null || Envir.Random.Next(4) == 0))
                    {
                        MapObject mon = FindMonsterAround(_leader.CurrentLocation, ViewRange, FollowLureRange);

                        // 找不到新的别把手上这只怪丢了（否则会一步三回头，看起来像卡住）
                        if (mon != null) _target = mon;
                        else if (_target != null && (_target.Dead || _target.Node == null)) _target = null;
                    }
                }
                else if (!pvpFight && (_target == null || _target.Dead || _target.Node == null || Envir.Random.Next(4) == 0))
                {
                    _target = FindMonster(ViewRange);
                }
            }

            // ---- 4.5 一个人闲逛的时候，偶尔找另一个假人切磋一下（假人之间随机 PK） ----
            if (_target == null && !following && _leader == null && Envir.Time >= _nextPKCheck)
            {
                _nextPKCheck = Envir.Time + 30000 + Envir.Random.Next(0, 60000);

                if (Settings.FakePlayerPK && Envir.Random.Next(100) < Settings.FakePlayerPKChance)
                    _target = FindDuelTarget(ViewRange);
            }

            if (_target != null && (_target.Dead || _target.Node == null || _target.CurrentMap != p.CurrentMap))
                _target = null;

            if (_target != null)
            {
                CombatUpdate(_target);
                ChatUpdate();
                return;
            }

            // ---- 5. 没怪：捡漏 ----
            if (Envir.Time >= _nextLoot)
            {
                _nextLoot = Envir.Time + LootDelay + Envir.Random.Next(0, 300);

                if (_lootTarget == null || _lootTarget.Node == null || _lootTarget.CurrentMap != p.CurrentMap)
                    _lootTarget = FindLoot(LootRange);
            }

            ItemObject loot = _lootTarget;

            if (loot != null && (loot.Node == null || loot.CurrentMap != p.CurrentMap))
            {
                loot = null;
                _lootTarget = null;
            }

            if (loot != null)
            {
                _state = FakeBotState.Loot;

                if (p.CurrentLocation == loot.CurrentLocation)
                {
                    p.PickUp();
                    TryEquipBetter();          // 捡完顺手看看能不能换上
                    _lootTarget = null;
                    _nextLoot = Envir.Time + 400;
                }
                else
                {
                    StepTo(loot.CurrentLocation);
                }

                ChatUpdate();
                return;
            }

            // ---- 6. 跟班 或 自己巡游 ----
            if (following)
            {
                FollowUpdate();
                ChatUpdate();
                return;
            }

            RoamUpdate();
            ChatUpdate();

            // ---- 7. 长时间在同一张图 → 偶尔换个地方（假装去别的图练级） ----
            if (Envir.Time >= _nextMapHop)
            {
                _nextMapHop = Envir.Time + Envir.Random.Next(600000, 1800000);
                TryChangeMap();
            }
        }

        // ==================================================================================
        //  组队 & 跟随
        // ==================================================================================
        /// <summary>当前是不是在「跟班模式」：跟着一个真人玩家下地图</summary>
        private bool IsFollowing => Settings.FakePlayerFollow && _leader != null && _leader.Node != null;

        /// <summary>
        /// 要不要「丢下一切先把队长追上」：
        ///   · 队长换图了 → 要；
        ///   · 同图但离得太远（> FollowCatchupRange）→ 要。
        /// 走路追不上的情况由 FollowUpdate 里的远距离传送兜底。
        /// </summary>
        private bool NeedCatchup()
        {
            PlayerObject leader = _leader;

            if (leader == null || leader.Node == null || leader.CurrentMap == null) return false;

            FakePlayerObject p = Player;

            if (leader.CurrentMap != p.CurrentMap) return true;

            return Functions.MaxDistance(p.CurrentLocation, leader.CurrentLocation) > FollowCatchupRange;
        }

        /// <summary>放下手上正在干的事（打怪 / 捡东西 / 打架），准备去追人</summary>
        private void DropTarget()
        {
            _target = null;
            _lootTarget = null;
            _assistTarget = null;
            _assistUntil = 0;
            _duelUntil = 0;
            _lastCombatTarget = null;
            StuckReset();
        }

        /// <summary>
        /// 维护组队状态：
        ///   1. 有人邀请 → 直接同意（假人永远答应，玩家拉人不用等确认）；
        ///   2. 组里已经没有在线的真人玩家 → 退组。**这一步必须做**：
        ///      组队列表是全队共用的同一个 List，假人下线后再上线是另一个对象了，
        ///      残留的引用会让别人的客户端一直看到一个已经不存在的队友（还可能空引用）；
        ///   3. 组队期间把攻击模式切成 Group（=打所有外人但绝不打队友），退组再切回 All。
        /// </summary>
        private void GroupUpdate()
        {
            FakePlayerObject p = Player;

            // 被邀请 → 接受。GroupInvitation 是服务端在玩家点「邀请」时直接设上的，
            // 假人收到的邀请包虽然被丢弃，但这个字段还在，所以这里能直接接上。
            if (p.GroupMembers == null && p.GroupInvitation != null)
            {
                p.GroupInvite(true);
                return;
            }

            if (p.GroupMembers == null)
            {
                _leader = null;
                _assistTarget = null;
                _assistUntil = 0;
                SetGroupMode(false);
                return;
            }

            // 找一个「在线的真人」当队长：按队伍顺序找，队长不在就顺位下一个人。
            PlayerObject leader = null;

            for (int i = 0; i < p.GroupMembers.Count; i++)
            {
                PlayerObject m = p.GroupMembers[i];
                if (m == null || m.Node == null) continue;
                if (m is FakePlayerObject) continue;      // 不跟假人走，假人自己会巡游

                leader = m;
                break;
            }

            if (leader == null)
            {
                // 组里只剩假人或已下线的人 → 退组
                if (p.GroupMembers.Count > 1) p.LeaveGroup();
                else p.GroupMembers = null;               // 兜底：只剩自己一个，直接解除（LeaveGroup 这种情形会越界）

                _leader = null;
                _assistTarget = null;
                _assistUntil = 0;
                SetGroupMode(false);
                return;
            }

            _leader = leader;
            SetGroupMode(true);
        }

        /// <summary>组队期间攻击模式切到 Group（打所有外人、不打队友），退组切回 All。</summary>
        private void SetGroupMode(bool grouped)
        {
            if (_wasGrouped == grouped) return;

            _wasGrouped = grouped;
            Player.AMode = grouped ? AttackMode.Group : AttackMode.All;
        }

        /// <summary>
        /// 跟班：队长换图就跟着换过去，离远了就走过去，就在旁边就站着看戏。
        /// 动手打怪的活由主流程负责（会优先打队长身边的怪），这里只管「跟住人」。
        /// </summary>
        private void FollowUpdate()
        {
            FakePlayerObject p = Player;
            PlayerObject leader = _leader;

            if (leader == null || leader.Node == null || leader.CurrentMap == null)
            {
                _leader = null;
                return;
            }

            // 队长换图了 → 跟过去（表现上就像跟着进了同一个传送门）
            if (leader.CurrentMap != p.CurrentMap)
            {
                _state = FakeBotState.Follow;

                if (Envir.Time >= _nextFollowTp)
                {
                    FakePlayerManager.TeleportNear(p, leader);
                    SetHome(p.CurrentLocation);
                    Restart();

                    _nextFollowTp = Envir.Time + 2000;   // 放在 Restart 之后，避免被它清零
                }

                return;
            }

            // 安全区里顺手「买」点药（假人不用仓库，补满就走）
            if (p.InSafeZone && Envir.Time >= _nextRefill)
            {
                _nextRefill = Envir.Time + 20000 + Envir.Random.Next(0, 20000);
                FakePlayerManager.RefillPotions(p);
            }

            _state = FakeBotState.Follow;

            int leadDist = Functions.MaxDistance(p.CurrentLocation, leader.CurrentLocation);

            if (leadDist <= 2) return;   // 已经贴着队长了

            // 追了半天还在老远（队长绕路跑 / 路被地形堵死）→ 直接落到队长旁边。
            // 宁可突兀一点，也不能把人跟丢 —— 跟丢的假人等于下线。
            if (leadDist > FollowTeleportRange && Envir.Time >= _nextFollowTp)
            {
                FakePlayerManager.TeleportNear(p, leader);
                _nextFollowTp = Envir.Time + 3000;
                StuckReset();
                return;
            }

            if (!CanAct()) return;

            StepTo(leader.CurrentLocation);
        }

        // ==================================================================================
        //  战斗
        // ==================================================================================
        private void CombatUpdate(MapObject target)
        {
            FakePlayerObject p = Player;

            if (!target.IsAttackTarget(p))
            {
                _target = null;
                return;
            }

            _state = FakeBotState.Combat;

            // 目标可以是怪（练级 / 还手），也可以是打我的真人玩家（PK 还击）
            MonsterObject mon = target as MonsterObject;
            HumanObject human = target as HumanObject;

            if (mon == null && human == null)
            {
                _target = null;
                return;
            }

            int currentHP = mon != null ? mon.HP : human.HP;

            // 人打人（假人互殴 / PK 还击）有时间上限：到点就收手，各回各家。
            // 不然两个血厚的假人能在野外对砍到天荒地老，看起来反而很怪。
            if (mon == null && human != null)
            {
                // 替队长打架：只要「仇」还在（队长还在打他 / 他还在打我们的人），就一直帮下去，
                // 不受 45 秒收手限制。仇没了再给 AssistHoldTime 的宽限，宽限过后就散。
                if (HasLeaderConflict(human))
                {
                    _assistTarget = human;
                    _assistUntil = Envir.Time + AssistHoldTime;
                }

                bool helping = human == _assistTarget && Envir.Time < _assistUntil;

                if (_duelUntil == 0) _duelUntil = Envir.Time + DuelTime;
                else if (Envir.Time >= _duelUntil)
                {
                    if (helping)
                    {
                        _duelUntil = Envir.Time + DuelTime;      // 队长还在打 → 接着帮
                    }
                    else
                    {
                        _duelUntil = 0;
                        _assistTarget = null;
                        _assistUntil = 0;
                        _nextPKCheck = Envir.Time + 60000 + Envir.Random.Next(0, 120000);
                        _target = null;
                        _lastCombatTarget = null;
                        StuckReset();
                        return;
                    }
                }
            }
            else
            {
                _duelUntil = 0;
            }

            // 换了目标：重置「打不打得动」的计时
            if (target != _lastCombatTarget)
            {
                _lastCombatTarget = target;
                _lastTargetHP = currentHP;
                _nextHPCheck = Envir.Time + 6000;
                _attackFails = 0;
            }
            else if (Envir.Time >= _nextHPCheck)
            {
                // 6 秒下来目标一滴血都没掉 → 打不动（隔着墙 / 打不中 / 太强），
                // 别傻站着了：换目标，换来的位置由找怪逻辑自然处理
                if (currentHP >= _lastTargetHP)
                {
                    // 替队长打架时放宽：对方也许只是皮厚，慢慢磨 —— 由上面的单挑时限兜底
                    if (human != null && human == _assistTarget && Envir.Time < _assistUntil)
                    {
                        _lastTargetHP = currentHP;
                        _nextHPCheck = Envir.Time + 10000;
                    }
                    else
                    {
                        _target = null;
                        StuckReset();
                        return;
                    }
                }
                else
                {
                    _lastTargetHP = currentHP;
                    _nextHPCheck = Envir.Time + 6000;
                }
            }

            // 冷却中就别白调 Walk/Attack（它们会各自回一个 S.UserLocation，纯浪费）
            if (!CanAct()) return;

            int dist = Functions.MaxDistance(p.CurrentLocation, target.CurrentLocation);

            // 道士：自己快挂了先给自己治病（视觉上也更像真人）
            if (_healSpell != Spell.None && p.HP * 100 / Math.Max(1, p.Stats[Stat.HP]) < 55)
            {
                UserMagic heal = p.GetMagic(_healSpell);

                if (heal != null && p.MP > heal.Info.BaseCost + heal.Level * heal.Info.LevelCost)
                {
                    p.Direction = Functions.DirectionFromPoint(p.CurrentLocation, target.CurrentLocation);
                    p.Magic(_healSpell, p.Direction, p.ObjectID, p.CurrentLocation);
                    return;
                }
            }

            // 面对面
            if (dist <= 1)
            {
                StuckReset();

                // 法师 / 道士 / 弓箭 贴脸了也照样放技能。
                // （怪是一定会凑上来的，要是这里直接走肉搏分支，他们的技能就等于白给。）
                bool attempted;
                if (TryCastAttack(target, dist, out attempted)) return;

                p.Direction = Functions.DirectionFromPoint(p.CurrentLocation, target.CurrentLocation);
                MeleeAttack(target);
                return;
            }

            // 远程职业：在射程内就放技能
            bool castAttempted;
            if (TryCastAttack(target, dist, out castAttempted))
                return;

            if (castAttempted)
            {
                // 技能丢了但没成功（蓝不够 / 客户端侧拒绝）：连着失败就别再原地狂丢了，
                // 要么凑近点，要么干脆换目标
                if (++_attackFails >= 3)
                {
                    _attackFails = 0;
                    _target = null;
                    StuckReset();
                    return;
                }
            }

            // 战士：直线 2 格直接刺杀
            if (_thrusting && dist == 2 && IsStraight(p.CurrentLocation, target.CurrentLocation))
            {
                StuckReset();
                p.Direction = Functions.DirectionFromPoint(p.CurrentLocation, target.CurrentLocation);
                p.Attack(p.Direction, Spell.Thrusting);
                return;
            }

            // 离得太远就当没看见（避免追着怪跑遍全图）
            if (dist > HomeRange)
            {
                _target = null;
                return;
            }

            // 打不到现在的位置 → 走过去（刺杀要直线、近战要贴脸，都是走这一步）
            if (dist >= _lastTargetDist) _stuckTicks++;
            else
            {
                _lastTargetDist = dist;
                _stuckTicks = 0;
            }

            if (_stuckTicks > 10)
            {
                _target = null;
                StuckReset();
                return;
            }

            StepTo(target.CurrentLocation);
        }

        private void StuckReset()
        {
            _lastTargetDist = int.MaxValue;
            _stuckTicks = 0;
            _stuckRoam = 0;
        }

        /// <summary>
        /// 队长和这个人现在还有没有「仇」：
        ///   1. leader.RecentPvPTarget / RecentPvPTime：队长最近用普攻或技能打了哪个玩家。
        ///      这个记录挂在队长自己身上，不会被假人自己的攻击顶掉，所以最可靠；
        ///   2. leader.LastHitter / LastHitTime：谁最近打了队长（10 秒窗口）；
        ///   3. 队里任何一个人（含假人自己）最近被这个人打过 —— 队伍里的人不能被白打。
        /// </summary>
        private bool HasLeaderConflict(HumanObject h)
        {
            if (h == null) return false;

            PlayerObject leader = _leader;
            if (leader == null || leader.Node == null) return false;

            if (leader.RecentPvPTarget == h && Envir.Time <= leader.RecentPvPTime) return true;
            if (leader.LastHitter == h && Envir.Time <= leader.LastHitTime) return true;

            List<PlayerObject> members = Player.GroupMembers;

            if (members != null)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    PlayerObject m = members[i];

                    if (m == null || m.Node == null) continue;
                    if (m.LastHitter != h) continue;
                    if (Envir.Time > m.LastHitTime) continue;

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 这个人是不是组队假人该打的对象：现在还有仇，或者刚刚打过、还在「咬住不放」的宽限期内
        /// （宽限期由 CombatUpdate 里的 HasLeaderConflict 续期，所以不会自己无限延长）。
        /// </summary>
        private bool IsAssisting(HumanObject h)
        {
            if (h == null) return false;
            if (_leader == null || _leader.Node == null) return false;

            if (HasLeaderConflict(h)) return true;

            return h == _assistTarget && Envir.Time < _assistUntil;
        }

        // ==================================================================================
        //  还手 & 躲避
        // ==================================================================================

        /// <summary>
        /// 被打时由 FakePlayerObject.Attacked 调用：优先还手打我的那个（怪 / 玩家 / 别的假人），
        /// 血太少先拉开距离。
        /// </summary>
        public void OnAttacked(MapObject attacker)
        {
            FakePlayerObject p = Player;

            if (p.Dead || attacker == null || attacker.Dead) return;

            // 还击对象：
            //   怪          → 一律还手；
            //   真人玩家    → 还手（安全区 / 和平地图里 IsAttackTarget 会拦住）；
            //   别的假人    → 开了「假人互殴」就还手（组队时同样还手：队伍里的人不能被白打）。
            bool shouldRetaliate;

            if (attacker is MonsterObject m)
                shouldRetaliate = m.IsAttackTarget(p);
            else if (attacker is FakePlayerObject)
                shouldRetaliate = Settings.FakePlayerPK && attacker.IsAttackTarget(p);
            else if (attacker is HumanObject)
                shouldRetaliate = attacker.IsAttackTarget(p);
            else
                shouldRetaliate = false;

            if (shouldRetaliate)
            {
                bool switchTarget = _target == null || _target.Dead || _target.Node == null
                                    || (_target.CurrentMap != attacker.CurrentMap)
                                    || Envir.Random.Next(100) < 70;

                if (switchTarget)
                {
                    _target = attacker;
                    _lastCombatTarget = null;   // 强制重置战斗计时
                    _dodgeUntil = 0;            // 能还手就先还手
                    StuckReset();

                    // 人和人之间的架（互殴 / PK 还击）给个时间上限，免得无休无止地对砍
                    if (!(attacker is MonsterObject)) _duelUntil = Envir.Time + DuelTime;
                }
            }

            // 低血量 → 触发躲避：往后撤几步，顺便给吃药留时间
            int maxHP = p.Stats[Stat.HP];
            if (maxHP > 0 && p.HP * 100 / maxHP < 30 && Envir.Time >= _dodgeUntil)
                _dodgeUntil = Envir.Time + Envir.Random.Next(2000, 4500);
        }

        /// <summary>躲避：远离当前威胁走位；远程职业边撤边继续输出（放风筝）。</summary>
        private void DodgeUpdate()
        {
            FakePlayerObject p = Player;
            _state = FakeBotState.Combat;

            MapObject threat = _target;
            if (threat == null || threat.Dead || threat.Node == null || threat.CurrentMap != p.CurrentMap)
            {
                _dodgeUntil = 0;
                return;
            }

            // 远程：撤退途中还能打就打
            int dist = Functions.MaxDistance(p.CurrentLocation, threat.CurrentLocation);
            bool attempted;
            TryCastAttack(threat, dist, out attempted);

            if (!CanAct()) return;

            // 朝远离威胁的方向走
            MirDirection away = Functions.DirectionFromPoint(threat.CurrentLocation, p.CurrentLocation);
            Point dest = Functions.PointMove(p.CurrentLocation, away, 2);
            StepTo(dest);
        }

        /// <summary>
        /// 射程内就丢攻击技能。attempted = 这一拍确实尝试施法了；
        /// 返回 true = 施法动作已完成。蓝不够时不硬丢，计入失败，
        /// 连续失败会让 CombatUpdate 放弃傻丢、改为走位或换目标。
        /// </summary>
        private bool TryCastAttack(MapObject target, int dist, out bool attempted)
        {
            FakePlayerObject p = Player;
            attempted = false;

            if (_attackSpell == Spell.None || _attackSpell == Spell.DoubleSlash) return false;

            UserMagic magic = p.GetMagic(_attackSpell);

            int range = magic?.Info?.Range ?? 0;

            if (range <= 0 || dist > range) return false;

            attempted = true;

            // 蓝不够就别丢了（Magic() 返回 void，只能提前自查）：计入失败，让上层走位 / 换目标
            int cost = magic.Info.BaseCost + magic.Level * magic.Info.LevelCost;
            if (p.MP <= cost)
            {
                _attackFails++;
                return false;
            }

            p.Direction = Functions.DirectionFromPoint(p.CurrentLocation, target.CurrentLocation);
            p.Magic(_attackSpell, p.Direction, target.ObjectID, target.CurrentLocation);

            StuckReset();
            _attackFails = 0;
            return true;
        }

        /// <summary>贴脸攻击。战士顺带处理半月，刺客用双重砍击。</summary>
        private void MeleeAttack(MapObject target)
        {
            FakePlayerObject p = Player;

            Spell spell = Spell.None;

            if (p.Class == MirClass.战士)
            {
                // 身边怪多的话，有半月就甩半月（视觉上也更像真人）
                if (p.GetMagic(Spell.CrossHalfMoon) != null && CountMonstersAround(1) >= 2)
                    spell = Spell.CrossHalfMoon;
                else if (p.GetMagic(Spell.HalfMoon) != null && CountMonstersAround(1) >= 2)
                    spell = Spell.HalfMoon;
            }
            else if (p.Class == MirClass.刺客 && _attackSpell == Spell.DoubleSlash)
            {
                spell = Spell.DoubleSlash;
            }

            p.Attack(p.Direction, spell);
        }

        private int CountMonstersAround(int range)
        {
            int n = 0;
            Map map = Player.CurrentMap;

            for (int y = Player.CurrentLocation.Y - range; y <= Player.CurrentLocation.Y + range; y++)
            {
                if (y < 0 || y >= map.Height) continue;

                for (int x = Player.CurrentLocation.X - range; x <= Player.CurrentLocation.X + range; x++)
                {
                    if (x < 0 || x >= map.Width) continue;
                    if (x == Player.CurrentLocation.X && y == Player.CurrentLocation.Y) continue;

                    Cell cell = map.GetCell(x, y);
                    if (cell.Objects == null) continue;

                    for (int i = 0; i < cell.Objects.Count; i++)
                    {
                        MapObject ob = cell.Objects[i];
                        if (ob.Race != ObjectType.Monster || ob.Dead) continue;
                        if (!ob.IsAttackTarget(Player)) continue;
                        n++;
                    }
                }
            }

            return n;
        }

        // ==================================================================================
        //  巡游 / 发呆
        // ==================================================================================
        private void RoamUpdate()
        {
            FakePlayerObject p = Player;

            // 还没到能动的时机（Walk/Run 有 600ms 冷却，而我们的思考节拍更细）
            if (!CanAct())
            {
                _state = FakeBotState.Roam;
                return;
            }

            // 已经走到目标点附近 → 有概率原地发呆几秒
            if (_roamTarget == Point.Empty || Functions.InRange(p.CurrentLocation, _roamTarget, 1))
            {
                if (_roamTarget != Point.Empty && Envir.Random.Next(100) < 35)
                {
                    _idleUntil = Envir.Time + Envir.Random.Next(2500, 16000);
                    _roamTarget = p.CurrentLocation;
                    return;
                }
            }

            if (Envir.Time >= _nextRoam || _roamTarget == Point.Empty)
            {
                _nextRoam = Envir.Time + RoamDelay;
                PickRoamTarget();
            }

            // 离家太远就慢慢往回走，避免假人跑到天涯海角
            if (Functions.MaxDistance(p.CurrentLocation, _homePoint) > HomeRange)
            {
                _roamTarget = _homePoint;
            }

            _state = FakeBotState.Roam;

            if (_roamTarget == Point.Empty) return;

            bool moved = StepTo(_roamTarget);

            if (moved)
            {
                _stuckRoam = 0;
                return;
            }

            // 连续走不动（被墙 / 障碍挡住）
            if (++_stuckRoam < 4) return;

            _stuckRoam = 0;
            _nextRoam = 0;

            if (Functions.MaxDistance(p.CurrentLocation, _homePoint) > HomeRange)
            {
                // 连回家的路都走不通 → 把「家」搬到现在这里，别再贴着墙干耗
                _homePoint = p.CurrentLocation;
            }
            else
            {
                _roamTarget = Point.Empty;   // 下一轮重新随机挑一个点
            }
        }

        private void PickRoamTarget()
        {
            Map map = Player.CurrentMap;
            if (map == null) return;

            for (int i = 0; i < 14; i++)
            {
                Point loc = new Point(
                    Player.CurrentLocation.X + Envir.Random.Next(-13, 14),
                    Player.CurrentLocation.Y + Envir.Random.Next(-13, 14));

                if (!map.ValidPoint(loc)) continue;
                if (loc == Player.CurrentLocation) continue;

                _roamTarget = loc;
                return;
            }

            _roamTarget = Player.CurrentLocation;
        }

        // ==================================================================================
        //  回城补给
        // ==================================================================================
        private void RestockUpdate()
        {
            FakePlayerObject p = Player;

            // 站着别动，等时间到
            if (Envir.Time < _restockUntil)
            {
                // 顺手在城里回满血蓝
                if (p.PercentHealth < 60) AutoPot();

                // 在城里聊天最自然
                ChatUpdate();
                return;
            }

            // 补满药
            _nextPot = 0;
            AutoPot();

            // 挑一张新图继续练级
            _state = FakeBotState.Roam;
            _target = null;
            _idleUntil = 0;
            _roamTarget = Point.Empty;

            FakePlayerManager.SendBotHunting(this, true);
        }

        // ==================================================================================
        //  自动喝药
        // ==================================================================================
        private void AutoPot()
        {
            if (Envir.Time < _nextPot) return;
            _nextPot = Envir.Time + PotDelay + Envir.Random.Next(0, 300);

            FakePlayerObject p = Player;

            int maxHP = p.Stats[Stat.HP];
            int maxMP = p.Stats[Stat.MP];

            bool needHP = maxHP > 0 && p.HP * 100 / maxHP < Envir.Random.Next(60, 80);
            bool needMP = maxMP > 0 && p.MP * 100 / maxMP < Envir.Random.Next(35, 55);

            // 身上还挂着「缓回」的量就别再灌了 —— 普通药是把回复量存进 PotHealthAmount
            // 慢慢放的，不挡一下会一秒一瓶（服务端自己的英雄自动吃药也是这么判的）。
            if (needHP && p.PotHealthAmount > 0) needHP = false;
            if (needMP && p.PotManaAmount > 0) needMP = false;

            if (!needHP && !needMP) return;

            UserItem potion = FindPotion(needHP, needMP);

            if (potion == null)
            {
                // 没药了：如果刚探到地上有药（主流程的「急救捡药」会去捡），就先别急着回城 ——
                // 捡一瓶回来比跑一趟城快得多，看起来也更像真人。
                ItemObject med = _medOnGround;

                if (med != null && med.Node != null && med.CurrentMap == p.CurrentMap) return;

                if (Envir.Random.Next(100) < 45)
                    EnterRestock();

                return;
            }

            p.UseItem(potion.UniqueID);
        }

        /// <summary>
        /// 缺什么找什么。「只缺血」的时候绝对不能返回蓝药 —— 蓝药的数值往往更大，
        /// 按总量排序会一直挑中蓝药，结果血永远回不上来、蓝还一直灌。
        /// </summary>
        private UserItem FindPotion(bool needHP, bool needMP)
        {
            FakePlayerObject p = Player;

            UserItem best = null;
            int bestValue = -1;

            for (int i = 0; i < p.Info.Inventory.Length; i++)
            {
                UserItem item = p.Info.Inventory[i];
                if (item == null || item.Info == null) continue;
                if (item.Info.Type != ItemType.药水) continue;

                // Shape 0 = 普通血蓝药（缓回）；1 = 太阳水（立刻回）；6/7 = 百分比药
                short shape = item.Info.Shape;
                if (shape != 0 && shape != 1 && shape != 6 && shape != 7) continue;

                // 职业不符的药不能喝（比如战士体力仙丹），选了也是白选
                if (((byte)item.Info.RequiredClass & (1 << (int)p.Class)) == 0) continue;

                bool percent = shape == 6 || shape == 7;

                int hp = item.GetTotal(Stat.HP) + (percent ? item.GetTotal(Stat.生命值数率) : 0);
                int mp = item.GetTotal(Stat.MP) + (percent ? item.GetTotal(Stat.法力值数率) : 0);

                if (needHP && hp <= 0) continue;
                if (needMP && mp <= 0) continue;

                // 血优先（命比蓝重要）
                int value = hp * (needHP ? 2 : 0) + mp * (needMP ? 1 : 0);
                if (value <= bestValue) continue;

                bestValue = value;
                best = item;
            }

            return best;
        }

        private void EnterRestock()
        {
            if (_state == FakeBotState.Restock) return;
            if (IsFollowing) return;   // 跟着队长的时候不请假回城，不然就把人跟丢了

            _state = FakeBotState.Restock;
            _restockUntil = Envir.Time + Envir.Random.Next(8000, 20000);
            _target = null;

            // 回城（表现上就是「用了张回城卷」）：走服务端原生 Teleport，旁观玩家看得到传送特效
            FakePlayerManager.SendBotToTown(this);
        }

        // ==================================================================================
        //  走位（与 HeroObject.MoveTo 同款：直线走，不行就左右绕）
        // ==================================================================================
        /// <summary>现在能不能做动作（Walk/Run/Attack 都有冷却，冷却期间别白调用）</summary>
        private bool CanAct()
        {
            return !Player.Dead && Envir.Time >= Player.ActionTime;
        }

        /// <returns>true 表示这一步真的移动了</returns>
        private bool StepTo(Point location)
        {
            FakePlayerObject p = Player;

            if (p.CurrentLocation == location) return false;

            MirDirection dir = Functions.DirectionFromPoint(p.CurrentLocation, location);

            if (p.Run(dir)) return true;
            if (p.Walk(dir)) return true;

            MirDirection d = dir;
            for (int i = 0; i < 4; i++)
            {
                d = Functions.NextDir(d);
                if (p.Walk(d)) return true;
            }

            d = dir;
            for (int i = 0; i < 4; i++)
            {
                d = Functions.PreviousDir(d);
                if (p.Walk(d)) return true;
            }

            return false;
        }

        private static bool IsStraight(Point a, Point b)
        {
            return a.X == b.X || a.Y == b.Y || Math.Abs(a.X - b.X) == Math.Abs(a.Y - b.Y);
        }

        // ==================================================================================
        //  扫描：找怪 / 找地面物品 / 找切磋对象
        // ==================================================================================
        /// <summary>别人（另一个假人）是不是正忙着 —— 正打着怪的时候别去撩他</summary>
        public bool HasTarget => _target != null;

        /// <summary>在指定的中心点附近找怪（跟班时用队长的位置当中心，好和队长打同一只怪）</summary>
        private MapObject FindMonsterAround(Point center, int range, int maxFromSelf)
        {
            FakePlayerObject p = Player;
            Map map = p.CurrentMap;
            if (map == null) return null;

            for (int d = 0; d <= range; d++)
            {
                for (int y = center.Y - d; y <= center.Y + d; y++)
                {
                    if (y < 0) continue;
                    if (y >= map.Height) break;

                    for (int x = center.X - d; x <= center.X + d; x += Math.Abs(y - center.Y) == d ? 1 : d * 2)
                    {
                        if (x < 0) continue;
                        if (x >= map.Width) break;

                        Cell cell = map.GetCell(x, y);
                        if (!cell.Valid || cell.Objects == null) continue;

                        for (int i = 0; i < cell.Objects.Count; i++)
                        {
                            MapObject ob = cell.Objects[i];

                            if (ob.Race != ObjectType.Monster) continue;
                            if (ob.Dead || ob.Hidden) continue;
                            if (ob.Master != null) continue;
                            if (ob is TownArcher) continue;
                            if (ob is IntelligentCreatureObject) continue;
                            if (!ob.IsAttackTarget(p)) continue;

                            // 别为了追一只怪跑得离队长太远（不然「跟随」就变成「各打各的」了）
                            if (Functions.MaxDistance(ob.CurrentLocation, p.CurrentLocation) > maxFromSelf) continue;

                            return ob;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 帮队长打架：在队长（须同图）周围找一个「该打的人」。
        /// 只认玩家 —— 真人和其他假人都算；怪物交给普通的找怪逻辑。
        /// 判据见 IsAssisting()：队长在打他，或者他刚打了队长。
        /// </summary>
        private MapObject FindAssistTarget(PlayerObject leader, int range)
        {
            FakePlayerObject p = Player;

            if (leader == null || leader.Node == null) return null;

            Map map = p.CurrentMap;

            if (map == null) return null;
            if (leader.CurrentMap != map) return null;     // 不同图：先把人追上，追上再帮
            if (map.Info.NoFight) return null;             // 和平地图打不起来
            if (p.InSafeZone) return null;                 // 安全区里出了手也会被服务端挡回来

            Point center = leader.CurrentLocation;

            for (int d = 0; d <= range; d++)
            {
                for (int y = center.Y - d; y <= center.Y + d; y++)
                {
                    if (y < 0) continue;
                    if (y >= map.Height) break;

                    for (int x = center.X - d; x <= center.X + d; x += Math.Abs(y - center.Y) == d ? 1 : d * 2)
                    {
                        if (x < 0) continue;
                        if (x >= map.Width) break;

                        Cell cell = map.GetCell(x, y);
                        if (!cell.Valid || cell.Objects == null) continue;

                        for (int i = 0; i < cell.Objects.Count; i++)
                        {
                            MapObject ob = cell.Objects[i];

                            if (ob.Race != ObjectType.Player) continue;   // 只帮打人，怪另说
                            if (ob.Dead || ob.Hidden) continue;
                            if (ob == p || ob == leader) continue;
                            if (ob.CurrentMap != map) continue;

                            HumanObject h = (HumanObject)ob;

                            // 自己人不打（队友都在 p.GroupMembers 里）
                            if (p.GroupMembers != null && p.GroupMembers.Contains(h)) continue;

                            // 安全区 / 和平地图 / 队友 这些都会被 IsAttackTarget 否掉
                            if (!h.IsAttackTarget(p)) continue;

                            if (!IsAssisting(h)) continue;

                            // 别为了帮架跑得离队长太远（不然「跟随」又变成「各打各的」）
                            if (Functions.MaxDistance(h.CurrentLocation, p.CurrentLocation) > range + 4) continue;

                            return h;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 挑一个「另一个假人」来切磋（假人之间的随机 PK）。
        /// 只在允许打架的地图、双方都不在安全区、对方也闲着的时候才挑；随缘一点，不是总要最近的。
        /// </summary>
        private MapObject FindDuelTarget(int range)
        {
            FakePlayerObject p = Player;
            Map map = p.CurrentMap;

            if (map == null) return null;
            if (map.Info.NoFight) return null;    // 和平地图不打架
            if (p.InSafeZone) return null;        // 安全区里根本出手不了，省点表情

            for (int d = 1; d <= range; d++)
            {
                for (int y = p.CurrentLocation.Y - d; y <= p.CurrentLocation.Y + d; y++)
                {
                    if (y < 0) continue;
                    if (y >= map.Height) break;

                    for (int x = p.CurrentLocation.X - d; x <= p.CurrentLocation.X + d; x += Math.Abs(y - p.CurrentLocation.Y) == d ? 1 : d * 2)
                    {
                        if (x < 0) continue;
                        if (x >= map.Width) break;

                        Cell cell = map.GetCell(x, y);
                        if (!cell.Valid || cell.Objects == null) continue;

                        for (int i = 0; i < cell.Objects.Count; i++)
                        {
                            MapObject ob = cell.Objects[i];

                            if (!(ob is FakePlayerObject other)) continue;   // 只和同类切磋，不主动撩真人
                            if (other == p || other.Dead || other.Hidden) continue;
                            if (other.Brain == null || other.Brain.HasTarget) continue;    // 人家正忙着
                            if (other.GroupMembers != null || other.GroupInvitation != null) continue;
                            if (other.InSafeZone) continue;
                            if (!other.IsAttackTarget(p)) continue;
                            if (Envir.Random.Next(100) < 60) continue;       // 就近也不一定撩，随缘

                            return other;
                        }
                    }
                }
            }

            return null;
        }

        private MapObject FindMonster(int range)
        {
            FakePlayerObject p = Player;
            Map map = p.CurrentMap;
            if (map == null) return null;

            for (int d = 0; d <= range; d++)
            {
                for (int y = p.CurrentLocation.Y - d; y <= p.CurrentLocation.Y + d; y++)
                {
                    if (y < 0) continue;
                    if (y >= map.Height) break;

                    for (int x = p.CurrentLocation.X - d; x <= p.CurrentLocation.X + d; x += Math.Abs(y - p.CurrentLocation.Y) == d ? 1 : d * 2)
                    {
                        if (x < 0) continue;
                        if (x >= map.Width) break;

                        Cell cell = map.GetCell(x, y);
                        if (!cell.Valid || cell.Objects == null) continue;

                        for (int i = 0; i < cell.Objects.Count; i++)
                        {
                            MapObject ob = cell.Objects[i];

                            if (ob.Race != ObjectType.Monster) continue;
                            if (ob.Dead || ob.Hidden) continue;
                            if (ob.Master != null) continue;      // 别的玩家的宝宝不打
                            if (ob is TownArcher) continue;       // 弓箭手卫兵不打
                            if (ob is IntelligentCreatureObject) continue;
                            if (!ob.IsAttackTarget(p)) continue;
                            if (d == 0) continue;                 // 和怪物叠在一起时先走开一步

                            return ob;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 找脚边能捡的东西。
        /// 同一圈里**优先捡药水和金币**，其次才是装备 —— 假人背包就那几格，
        /// 见什么捡什么的话很快就塞满垃圾，真正要用的药反而捡不进来。
        /// </summary>
        private ItemObject FindLoot(int range)
        {
            FakePlayerObject p = Player;
            Map map = p.CurrentMap;
            if (map == null) return null;

            for (int d = 0; d <= range; d++)
            {
                ItemObject anyLoot = null;

                for (int y = p.CurrentLocation.Y - d; y <= p.CurrentLocation.Y + d; y++)
                {
                    if (y < 0) continue;
                    if (y >= map.Height) break;

                    for (int x = p.CurrentLocation.X - d; x <= p.CurrentLocation.X + d; x += Math.Abs(y - p.CurrentLocation.Y) == d ? 1 : d * 2)
                    {
                        if (x < 0) continue;
                        if (x >= map.Width) break;

                        Cell cell = map.GetCell(x, y);
                        if (!cell.Valid || cell.Objects == null) continue;

                        for (int i = 0; i < cell.Objects.Count; i++)
                        {
                            MapObject ob = cell.Objects[i];
                            if (ob.Race != ObjectType.Item) continue;

                            ItemObject io = (ItemObject)ob;
                            if (io.Owner != null && io.Owner != p) continue;

                            if (io.Item != null && !p.CanGainItem(io.Item)) continue;

                            // 药水 / 金币 —— 这一圈里看到就直接拿走
                            if (IsPotion(io) || IsGold(io)) return io;

                            if (anyLoot == null) anyLoot = io;
                        }
                    }
                }

                // 这一圈里没有药，那就退一步把装备也捡了（总比空手强）
                if (anyLoot != null) return anyLoot;
            }

            return null;
        }

        /// <summary>只要药水（身上没药时的急救通道用）</summary>
        private ItemObject FindPotionOnGround(int range)
        {
            FakePlayerObject p = Player;
            Map map = p.CurrentMap;
            if (map == null) return null;

            for (int d = 0; d <= range; d++)
            {
                for (int y = p.CurrentLocation.Y - d; y <= p.CurrentLocation.Y + d; y++)
                {
                    if (y < 0) continue;
                    if (y >= map.Height) break;

                    for (int x = p.CurrentLocation.X - d; x <= p.CurrentLocation.X + d; x += Math.Abs(y - p.CurrentLocation.Y) == d ? 1 : d * 2)
                    {
                        if (x < 0) continue;
                        if (x >= map.Width) break;

                        Cell cell = map.GetCell(x, y);
                        if (!cell.Valid || cell.Objects == null) continue;

                        for (int i = 0; i < cell.Objects.Count; i++)
                        {
                            MapObject ob = cell.Objects[i];
                            if (ob.Race != ObjectType.Item) continue;

                            ItemObject io = (ItemObject)ob;
                            if (!IsPotion(io)) continue;
                            if (io.Owner != null && io.Owner != p) continue;
                            if (!p.CanGainItem(io.Item)) continue;

                            return io;
                        }
                    }
                }
            }

            return null;
        }

        private static bool IsPotion(ItemObject io)
        {
            return io.Item != null && io.Item.Info != null && io.Item.Info.Type == ItemType.药水;
        }

        private static bool IsGold(ItemObject io)
        {
            return io.Item == null && io.Gold > 0;
        }

        /// <summary>数一下背包里还剩几瓶血药 / 蓝药（药水都是可堆叠的，所以数的是「瓶数」）</summary>
        private void CountPotions(out int hpCount, out int mpCount)
        {
            hpCount = 0;
            mpCount = 0;

            UserItem[] inv = Player.Info.Inventory;

            for (int i = 0; i < inv.Length; i++)
            {
                UserItem item = inv[i];
                if (item == null || item.Info == null) continue;
                if (item.Info.Type != ItemType.药水) continue;

                int hp = item.GetTotal(Stat.HP) + item.GetTotal(Stat.生命值数率);
                int mp = item.GetTotal(Stat.MP) + item.GetTotal(Stat.法力值数率);

                // 既能回血又能回蓝的（比如太阳水）算血药 —— 保命优先
                if (hp > 0) hpCount += item.Count;
                else if (mp > 0) mpCount += item.Count;
            }
        }

        // ==================================================================================
        //  背包清理：快满的时候把杂物丢掉（假人不会像真人那样去仓库）
        // ==================================================================================
        private void CleanBag()
        {
            if (Envir.Time < _nextBagCheck) return;

            UserItem[] inv = Player.Info.Inventory;

            int free = 0;
            for (int i = 0; i < inv.Length; i++)
                if (inv[i] == null) free++;

            // 背包越满查得越勤：空地少的时候 3 秒看一次，宽裕的时候十几秒一次。
            // 假人不会去仓库，格位就是硬上限 —— 腾不出地方，地上的药就永远捡不进来。
            _nextBagCheck = Envir.Time + (free < 5 ? 3000 : 12000 + Envir.Random.Next(0, 8000));

            if (free >= 5) return;

            for (int n = 0; n < 5 && free < 5; n++)
            {
                int worst = -1;
                int worstScore = int.MaxValue;

                for (int i = 0; i < inv.Length; i++)
                {
                    UserItem item = inv[i];
                    if (item == null) continue;
                    if (IsWorthKeeping(item)) continue;
                    if (item.Info.Bind.HasFlag(BindMode.DontDrop)) continue;

                    int score = ItemScore(item);
                    if (score >= worstScore) continue;

                    worstScore = score;
                    worst = i;
                }

                if (worst < 0) break;

                UserItem junk = inv[worst];
                Player.DropItem(junk.UniqueID, junk.Count, false);
                free++;
            }
        }

        /// <summary>背包还剩几个空格</summary>
        private int FreeBagSpace()
        {
            UserItem[] inv = Player.Info.Inventory;

            int free = 0;
            for (int i = 0; i < inv.Length; i++)
                if (inv[i] == null) free++;

            return free;
        }

        /// <summary>这件东西值不值得留着（药水 / 护身符 / 能穿的装备 / 比身上强的装备）</summary>
        private bool IsWorthKeeping(UserItem item)
        {
            if (item.Info == null) return false;

            switch (item.Info.Type)
            {
                case ItemType.药水:
                case ItemType.护身符:
                    return true;
            }

            int slot = SlotFor(item);
            if (slot < 0) return false;                       // 非装备（杂物）→ 可以扔

            if (!Player.CanEquipItem(item, slot)) return false;

            UserItem worn = Player.Info.Equipment[slot];
            if (worn == null) return true;

            return ItemScore(item) > ItemScore(worn);
        }

        // ==================================================================================
        //  捡到更好的装备就穿上
        // ==================================================================================
        private void TryEquipBetter()
        {
            FakePlayerObject p = Player;

            // 从后往前扫背包，先处理价值最高的
            for (int i = 0; i < p.Info.Inventory.Length; i++)
            {
                UserItem item = p.Info.Inventory[i];
                if (item == null) continue;

                int slot = SlotFor(item);
                if (slot < 0) continue;
                if (!p.CanEquipItem(item, slot)) continue;

                UserItem worn = p.Info.Equipment[slot];

                if (worn != null && ItemScore(worn) >= ItemScore(item)) continue;

                p.EquipItem(MirGridType.Inventory, item.UniqueID, slot);
                return;   // 一次只换一件，避免瞬间换一身显得很假
            }
        }

        /// <summary>物品 → 应装备的槽位（-1 表示不是装备）</summary>
        private static int SlotFor(UserItem item)
        {
            switch (item.Info.Type)
            {
                case ItemType.武器: return (int)EquipmentSlot.武器;
                case ItemType.盔甲: return (int)EquipmentSlot.盔甲;
                case ItemType.头盔: return (int)EquipmentSlot.头盔;
                case ItemType.项链: return (int)EquipmentSlot.项链;
                case ItemType.手镯: return (int)EquipmentSlot.左手镯;
                case ItemType.戒指: return (int)EquipmentSlot.左戒指;
                case ItemType.腰带: return (int)EquipmentSlot.腰带;
                case ItemType.靴子: return (int)EquipmentSlot.靴子;
                case ItemType.守护石: return (int)EquipmentSlot.守护石;
                case ItemType.照明物: return (int)EquipmentSlot.照明物;
                default: return -1;
            }
        }

        /// <summary>粗略评分：谁强穿谁（只看攻防，够用）</summary>
        public static int ItemScore(UserItem item)
        {
            if (item == null || item.Info == null) return 0;

            return item.GetTotal(Stat.MaxDC)
                 + item.GetTotal(Stat.MaxMC)
                 + item.GetTotal(Stat.MaxSC)
                 + item.GetTotal(Stat.MaxAC) * 2
                 + item.GetTotal(Stat.MaxMAC) * 2
                 + item.GetTotal(Stat.HP) / 2
                 + item.GetTotal(Stat.准确);
        }

        // ==================================================================================
        //  聊天 / 喊话
        // ==================================================================================
        private void ChatUpdate()
        {
            if (!FakePlayerManager.ChatEnabled) return;
            if (Envir.Time < _nextChat) return;

            _nextChat = Envir.Time + Envir.Random.Next(60000, 300000);

            // 不在战斗中才聊（在打架还打字很假）
            if (_state == FakeBotState.Combat) return;

            string line = FakePlayerManager.RandomChatLine();
            if (string.IsNullOrWhiteSpace(line)) return;

            if (Envir.Random.Next(100) < 12)
                Player.Chat("!" + line);       // 偶尔喊话，全屏可见
            else
                Player.Chat(line);             // 平时普通说话
        }

        // ==================================================================================
        //  换图
        // ==================================================================================
        private void TryChangeMap()
        {
            // 回城假装补给，补给完由 RestockUpdate 挑新图
            EnterRestock();
        }

        /// <summary>补给结束时由管理器指定下一张练级图（true = 用回城卷回去）</summary>
        internal void SetHome(Point point)
        {
            _homePoint = point;
            _roamTarget = Point.Empty;
        }

        /// <summary>刚上线 / 刚传送到新图：清空状态，从头开始</summary>
        internal void Restart()
        {
            _state = FakeBotState.Roam;
            _target = null;
            _lootTarget = null;
            _roamTarget = Point.Empty;
            _nextUpdate = 0;
            _nextSearch = 0;
            _nextRoam = 0;
            _nextLoot = 0;
            _nextPot = 0;
            _nextMedCheck = 0;
            _medOnGround = null;
            _nextFollowTp = 0;
            _idleUntil = 0;
            _restockUntil = 0;
            _dodgeUntil = 0;
            _duelUntil = 0;
            _assistTarget = null;
            _assistUntil = 0;
            _lastCombatTarget = null;
            StuckReset();
        }

        /// <summary>先站着别动一段时间（模拟刚进游戏 / 刚回城的停顿）</summary>
        internal void BeginIdle(long ms)
        {
            if (ms <= 0) return;

            _idleUntil = Envir.Time + ms;
            _nextUpdate = 0;
            _state = FakeBotState.Idle;
        }
    }
}
