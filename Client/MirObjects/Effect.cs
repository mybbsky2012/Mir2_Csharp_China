using Client.MirGraphics;
using Client.MirScenes;
using Client.MirSounds;

namespace Client.MirObjects
{
    public class Effect
    {
        public MLibrary Library;

        public int BaseIndex, Count, Duration;
        public long Start;

        public int CurrentFrame;
        public long NextFrame;

        public Point Source;
        public MapObject Owner;

        public int Light = 6;
        public Color LightColour = Color.White;
        /// <summary>顶点色（含 alpha）。加法混合下实际强度 = 纹理alpha × 该顶点色alpha，改这里可整体调暗/调亮。</summary>
        public Color DrawColour = Color.White;
        /// <summary>自定义帧序列（用于素材中间夹空帧的情况，如落雷杀 1770-1809）。非空时按此列表取帧，Count 需等于其长度。</summary>
        public int[] FrameList;

        public bool Blend = true;
        public float Rate = 1F;
        public Point DrawLocation;
        public Point DrawOffset = Point.Empty;
        public bool Repeat;
        public long RepeatUntil;

        public bool DrawBehind = false;

        public long CurrentDelay;
        public long Delay;

        public event EventHandler Complete;
        public event EventHandler Played;

        #region 特效限流（10-31）
        // 原来客户端对特效完全没有数量上限：满屏怪物同时放技能时特效能涨到几百个，
        // 而每个特效每推进一帧都会把 MapControl.TextureValid 置 false（= 下一帧整屏重建），
        // 夜晚还要各自再叠一张 605x460 的加法混合光斑 —— 这是「满屏 + 齐射就卡死」的主体。
        //
        // 这里在【构造阶段】就拦截，而不是等到绘制时再跳过：超限的特效第一次 Process 就自我移除，
        // 既不进绘制列表、也不会把画面置脏。先来的优先，已经存在的特效完全不受影响。
        //   · 只限「一次性技能特效」（Effect / SpecialEffect）；
        //     Buff / 元素珠 / 持续爆炸 / 飞弹 / 纯光源这些有状态的特效必须保留，否则表现会缺一大块。
        /// <summary>全局同时存在的技能特效上限。</summary>
        public const int MaxActiveEffects = 100;
        /// <summary>单个对象身上同时叠加的技能特效上限。</summary>
        public const int MaxEffectsPerOwner = 8;
        /// <summary>同一个对象、同一素材在该时间窗内只保留一个（毫秒）。</summary>
        public const int DuplicateWindowMs = 300;

        /// <summary>被限流丢弃：不绘制、立即移除、不触发 Complete。</summary>
        public bool Suppressed;

        private static bool IsLimitable(Effect e)
        {
            Type t = e.GetType();
            return t == typeof(Effect) || t == typeof(SpecialEffect);
        }

        /// <summary>
        /// 单对象限流：在构造阶段判断，不用等绘制。只看自己身上的特效，不依赖任何全局计数。
        /// </summary>
        private void RegisterLimit()
        {
            if (!IsLimitable(this)) return;
            if (Owner == null || Owner.Effects == null) return;

            int same = 0;

            for (int i = 0; i < Owner.Effects.Count; i++)
            {
                Effect other = Owner.Effects[i];
                if (other == null || other.Suppressed) continue;
                same++;

                // 同一只怪、同一套素材、连发时只留第一发：群攻技能经常一次给同一个目标叠好几层同图特效
                if (other.Library == Library && other.BaseIndex == BaseIndex &&
                    CMain.Time - other.Start < DuplicateWindowMs)
                {
                    Suppressed = true;
                    return;
                }
            }

            if (same >= MaxEffectsPerOwner)
                Suppressed = true;
        }

        private static readonly List<Effect> _limitBuffer = new List<Effect>();

        /// <summary>上一帧统计到的技能特效数量（调试面板显示用）。</summary>
        public static int LastLiveCount;

        /// <summary>
        /// 全局限流：每帧由 MapControl.Process 调一次，按「实际在场数量」校正，而不是靠构造时加减计数。
        /// 靠计数容易漂移（切图时 Effects.Clear()、怪物阶段切换时 Effects.Clear() 都不会走 Remove），
        /// 一旦漂高就会把所有新特效永久抑制掉，所以这里每帧重新数一遍：数量很小，开销可以忽略。
        /// </summary>
        public static void EnforceGlobalLimit()
        {
            _limitBuffer.Clear();

            for (int i = 0; i < MapControl.Effects.Count; i++)
            {
                Effect e = MapControl.Effects[i];
                if (e != null && !e.Suppressed && IsLimitable(e)) _limitBuffer.Add(e);
            }

            for (int i = 0; i < MapControl.Objects.Count; i++)
            {
                MapObject ob = MapControl.Objects[i];
                if (ob == null || ob.Effects == null) continue;

                for (int j = 0; j < ob.Effects.Count; j++)
                {
                    Effect e = ob.Effects[j];
                    if (e != null && !e.Suppressed && IsLimitable(e)) _limitBuffer.Add(e);
                }
            }

            int over = _limitBuffer.Count - MaxActiveEffects;
            LastLiveCount = _limitBuffer.Count;
            if (over <= 0) return;

            // 分两轮丢：第一轮只动「怪物/地图」特效，玩家、英雄、其他玩家身上的特效留到最后。
            // 否则自己在怪堆里放技能时，刚建出来的特效反而会先被裁掉，手感会很怪。
            for (int pass = 0; pass < 2 && over > 0; pass++)
            {
                for (int i = _limitBuffer.Count - 1; i >= 0 && over > 0; i--)
                {
                    Effect e = _limitBuffer[i];
                    if (e.Suppressed) continue;

                    bool important = e.Owner != null &&
                                     (e.Owner == MapObject.User || e.Owner == MapObject.HeroObject ||
                                      e.Owner.Race == ObjectType.Player || e.Owner.Race == ObjectType.Hero);

                    if (pass == 0 && important) continue;

                    e.Suppressed = true;
                    over--;
                }
            }
        }
        #endregion

        public Effect(MLibrary library, int baseIndex, int count, int duration, MapObject owner, long starttime = 0, bool drawBehind = false)
        {
            Library = library;
            BaseIndex = baseIndex;
            Count = count == 0 ? 1 : count;
            Duration = duration;
            Start = starttime == 0 ? CMain.Time : starttime;

            NextFrame = Start + (Duration / Count) * (CurrentFrame + 1);
            Owner = owner;
            Source = Owner.CurrentLocation;

            DrawBehind = drawBehind;

            RegisterLimit();
        }
        public Effect(MLibrary library, int baseIndex, int count, int duration, Point source, long starttime = 0, bool drawBehind = false)
        {
            Library = library;
            BaseIndex = baseIndex;
            Count = count == 0 ? 1 : count;
            Duration = duration;
            Start = starttime == 0 ? CMain.Time : starttime;

            NextFrame = Start + (Duration / Count) * (CurrentFrame + 1);
            Source = source;

            DrawBehind = drawBehind;

            RegisterLimit();
        }

        public void SetStart(long start)
        {
            Start = start;

            NextFrame = Start + (Duration / Count) * (CurrentFrame + 1);
        }

        public virtual void Process()
        {
            //被限流丢弃的特效：第一次 Process 就摘掉自己，绝不把画面置脏
            if (Suppressed)
            {
                Remove();
                return;
            }

            if (CurrentFrame == 1)
            {
                if (Played != null)
                    Played(this, EventArgs.Empty);
            }

            if (CMain.Time <= NextFrame) return;

            if (Owner != null && Owner.SkipFrames) CurrentFrame++;

            if (++CurrentFrame >= Count)
            {
                if (Repeat && (RepeatUntil == 0 || CMain.Time < RepeatUntil))
                {
                    CurrentFrame = 0;
                    Start = CMain.Time + Delay;
                    NextFrame = Start + (Duration / Count) * (CurrentFrame + 1);
                }
                else
                    Remove();
            }
            else NextFrame = Start + (Duration / Count) * (CurrentFrame + 1);

            GameScene.Scene.MapControl.TextureValid = false;
        }

        public virtual void Remove()
        {
            if (Owner != null)
                Owner.Effects.Remove(this);
            else
                MapControl.Effects.Remove(this);

            //被限流丢弃的特效不触发 Complete：否则可能连带再去建新特效/放音效，等于白限
            if (Suppressed) return;

            if (Complete != null)
                Complete(this, EventArgs.Empty);
        }

        public virtual void Draw()
        {
            if (Suppressed) return;

            if (CMain.Time < Start) return;

            if (Owner != null)
            {
                DrawLocation = Owner.DrawLocation;
            }
            else
            {
                DrawLocation = new Point((Source.X - MapObject.User.Movement.X + MapControl.OffSetX) * MapControl.CellWidth,
                                         (Source.Y - MapObject.User.Movement.Y + MapControl.OffSetY) * MapControl.CellHeight);
                DrawLocation.Offset(DrawOffset);
                DrawLocation.Offset(MapObject.User.OffSetMove);
            }


            //素材中间可能夹空帧（如落雷杀 1770-1809），此时按 FrameList 取帧
            int index = (FrameList != null && CurrentFrame < FrameList.Length) ? FrameList[CurrentFrame] : BaseIndex + CurrentFrame;

            if (Blend)
                Library.DrawBlend(index, DrawLocation, DrawColour, true, Rate);
            else
                Library.Draw(index, DrawLocation, DrawColour, true);
        }

        public void Clear()
        {
            Complete = null;
            Played = null;
        }
    }

    public class Missile : Effect
    {
        public static List<Missile> Missiles = new List<Missile>();
        public MapObject Target;
        public Point Destination;
        public int Interval, FrameCount, Skip;
        public int Direction;
        public bool Explode;


        public Missile(MLibrary library, int baseIndex, int count, int duration, MapObject owner, Point target, bool direction16 = true)
            : base(library, baseIndex, count, duration, owner)
        {
            Missiles.Add(this);
            Source = Owner.CurrentLocation;
            Destination = target;
            Direction = direction16 ? MapControl.Direction16(Source, Destination) : (int)Functions.DirectionFromPoint(Source, Destination);
        }

        public Missile(MLibrary library, int baseIndex, int count, int duration, Point source, Point target)
            : base(library, baseIndex, count, duration, source)
        {
            Missiles.Add(this);
            Destination = target;

            Direction = MapControl.Direction16(Source, Destination);


        }

        public override void Process()
        {
            if (CMain.Time < Start) return;

            if (Target != null) Destination = Target.CurrentLocation;
            else if (!Explode)
            {
                int dist = Functions.MaxDistance(Owner.CurrentLocation, Destination);

                if (dist < Globals.DataRange)
                    Destination.Offset(Destination.X - Source.X, Destination.Y - Source.Y);
            }

            Duration = Functions.MaxDistance(Source, Destination) * 50;
            Count = Duration / Interval;
            if (Count == 0) Count = 1;

            base.Process();
        }
        public override void Remove()
        {
            base.Remove();
            Missiles.Remove(this);
        }
        public override void Draw()
        {
            if (CMain.Time < Start) return;


            int index = BaseIndex + (CurrentFrame % FrameCount) + Direction * (Skip + FrameCount);

            DrawLocation = new Point((Source.X - MapObject.User.Movement.X + MapControl.OffSetX) * MapControl.CellWidth,
                                       (Source.Y - MapObject.User.Movement.Y + MapControl.OffSetY) * MapControl.CellHeight);
            DrawLocation.Offset(MapObject.User.OffSetMove);

            int x = (Destination.X - Source.X) * MapControl.CellWidth;
            int y = (Destination.Y - Source.Y) * MapControl.CellHeight;


            DrawLocation.Offset(x * CurrentFrame / Count, y * CurrentFrame / Count);

            if (!Blend)
                Library.Draw(index, DrawLocation, Color.White, true);
            else
                Library.DrawBlend(index, DrawLocation, Color.White, true, Rate);
        }

    }

    public class InterruptionEffect : Effect
    {
        public static List<InterruptionEffect> effectlist = new List<InterruptionEffect>();
        bool noProcess = false;

        public InterruptionEffect(MLibrary library, int baseIndex, int count, int duration, MapObject owner, bool blend, long starttime = 0)
            : base(library, baseIndex, count, duration, owner)
        {
            Repeat = true;
            Blend = blend;
            effectlist.Add(this);
        }

        public override void Process()
        {
            if (!noProcess)
                base.Process();
        }

        public override void Remove()
        {
            base.Remove();
            effectlist.Remove(this);
        }

        public override void Draw()
        {
            if (!((PlayerObject)Owner).Concentrating)
                Remove();
            else if (((PlayerObject)Owner).ConcentrateInterrupted)
                noProcess = true;
            else
                noProcess = false;
            if (!noProcess)
                base.Draw();
        }

        public static int GetOwnerEffectID(uint objectID)
        {
            for (int i = 0; i < effectlist.Count; i++)
            {
                if (effectlist[i].Owner.ObjectID != objectID) continue;
                return i;
            }
            return -1;
        }
    }

    public class ElementsEffect : Effect
    {
        int myType;//1 = green orb, 2 = blue orb, 3 = red orb, 4 = mixed orbs
        long killAt;//holds the exp value for 4 orbs : kills all orbs when myType 4 is reached
        bool loopit = false;//soundloop

        public ElementsEffect(MLibrary library, int baseIndex, int count, int duration, MapObject owner, bool blend, int elementType, int killtime, bool loopon = false)
            : base(library, baseIndex, count, duration, owner)
        {
            Repeat = true;
            Blend = blend;
            myType = elementType;
            killAt = killtime;
            //
            loopit = loopon;
            StopSounds();
            StartSound();
        }

        public override void Process()
        {

            base.Process();
        }

        private void StartSound()
        {
            SoundManager.PlaySound(20000 + 126 * 10 + 4 + myType, loopit);
        }

        private void StopSounds()
        {
            for (int i = 0; i <= 3; i++)
                SoundManager.StopSound(20000 + 126 * 10 + 5 + i);
        }

        public override void Remove()
        {
            SoundManager.StopSound(20000 + 126 * 10 + 4 + myType);
            base.Remove();
        }

        public override void Draw()
        {
            if (!((PlayerObject)Owner).HasElements)
                Remove();
            if (((PlayerObject)Owner).ElementsLevel >= killAt && myType < 4)
                Remove();
            base.Draw();
        }
    }

    public class DelayedExplosionEffect : Effect
    {
        public static List<DelayedExplosionEffect> effectlist = new List<DelayedExplosionEffect>();
        public int stage;

        public DelayedExplosionEffect(MLibrary library, int baseIndex, int count, int duration, MapObject owner, bool blend, int Stage, long until)
            : base(library, baseIndex, count, duration, owner)
        {
            Repeat = (Stage == 2) ? false : true;
            Blend = blend;
            stage = Stage;
            effectlist.Add(this);
            if (stage == 1)
                SoundManager.PlaySound(20000 + 125 * 10);
            if (stage == 2)
                SoundManager.PlaySound(20000 + 125 * 10 + 5);
        }

        public override void Process()
        {
            base.Process();
        }

        public override void Remove()
        {
            base.Remove();
            effectlist.Remove(this);
        }

        public override void Draw()
        {
            if (Owner.Dead)
            {
                Remove();
                return;
            }
            base.Draw();
        }

        public static int GetOwnerEffectID(uint objectID)
        {
            for (int i = 0; i < effectlist.Count; i++)
            {
                if (effectlist[i].Owner.ObjectID != objectID) continue;
                return i;
            }
            return -1;
        }
    }

    public class SpecialEffect : Effect
    {
        public uint EffectType = 0;

        public SpecialEffect(MLibrary library, int baseIndex, int count, int duration, MapObject owner, bool blend, bool drawBehind, uint type)
            : base(library, baseIndex, count, duration, owner, 0, drawBehind)
        {
            Blend = blend;
            DrawBehind = drawBehind;
            EffectType = type;
            Light = -1;
        }

        public override void Process()
        {
            base.Process();
        }
    }

    public class BuffEffect : Effect
    {
        public BuffType BuffType;

        public BuffEffect(MLibrary library, int baseIndex, int count, int duration, MapObject owner, bool blend, BuffType buffType, Color? drawColour = null)
            : base(library, baseIndex, count, duration, owner, 0)
        {
            Repeat = true;
            Blend = blend;
            BuffType = buffType;
            Light = -1;
            DrawColour = drawColour ?? Color.White;
        }

        public override void Process()
        {
            base.Process();
        }

        public override void Draw()
        {
            if (CMain.Time < Start) return;

            if (Owner != null)
            {
                DrawLocation = Owner.DrawLocation;
            }
            else
            {
                DrawLocation = new Point((Source.X - MapObject.User.Movement.X + MapControl.OffSetX) * MapControl.CellWidth,
                                         (Source.Y - MapObject.User.Movement.Y + MapControl.OffSetY) * MapControl.CellHeight);
                DrawLocation.Offset(DrawOffset);
                DrawLocation.Offset(MapObject.User.OffSetMove);
            }

            if (Blend)
                Library.DrawBlend(BaseIndex + CurrentFrame, DrawLocation, DrawColour, true, Rate);
            else
                Library.Draw(BaseIndex + CurrentFrame, DrawLocation, DrawColour, true);
        }
    }

    public class TrackableEffect : Effect
    {
        public static List<TrackableEffect> effectlist = new List<TrackableEffect>();
        public string EffectName = "default";

        public TrackableEffect(Effect baseEffect, string effName = "null")
            : base(baseEffect.Library, baseEffect.BaseIndex, baseEffect.Count, baseEffect.Duration, baseEffect.Owner, baseEffect.Start)
        {
            Repeat = baseEffect.Repeat;
            RepeatUntil = baseEffect.RepeatUntil;
            Blend = baseEffect.Blend;

            EffectName = effName;

            effectlist.Add(this);
        }

        public static int GetOwnerEffectID(uint objectID, string effectName = "null")
        {
            for (int i = 0; i < effectlist.Count; i++)
            {
                if (effectlist[i].Owner.ObjectID != objectID) continue;
                if (effectName != "null" && effectlist[i].EffectName != effectName) continue;
                return i;
            }
            return -1;
        }

        public override void Process()
        {
            base.Process();

            if (Owner == null) Remove();
            else if (Owner.Dead) Remove();
        }

        public override void Remove()
        {
            base.Remove();
            effectlist.Remove(this);
        }

        public void RemoveNoComplete()
        {
            if (Owner != null)
                Owner.Effects.Remove(this);
            else
                MapControl.Effects.Remove(this);
        }

    }

    public class LightEffect : Effect
    {
        public LightEffect(int duration, MapObject owner, long starttime = 0, int lightDistance = 6, Color? lightColour = null)
            : base(null, 0, 0, duration, owner, starttime)
        {
            Light = lightDistance;
            LightColour = lightColour == null ? Color.White : (Color)lightColour;
        }

        public LightEffect(int duration, Point source, long starttime = 0, int lightDistance = 6, Color? lightColour = null)
            : base(null, 0, 0, duration, source, starttime)
        {
            Light = lightDistance;
            LightColour = lightColour == null ? Color.White : (Color)lightColour;
        }

        public override void Process()
        {
            if (CMain.Time >= Start + Duration)
                Remove();
            GameScene.Scene.MapControl.TextureValid = false;

        }

        public override void Draw()
        {

            if (CMain.Time < Start) return;


            if (Owner != null)
            {
                DrawLocation = Owner.DrawLocation;
            }
            else
            {
                DrawLocation = new Point((Source.X - MapObject.User.Movement.X + MapControl.OffSetX) * MapControl.CellWidth,
                                         (Source.Y - MapObject.User.Movement.Y + MapControl.OffSetY) * MapControl.CellHeight);
                DrawLocation.Offset(MapObject.User.OffSetMove);
            }
        }
    }

}
