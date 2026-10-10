using Server.MirEnvir;

namespace Server.MirObjects
{
    public enum DelayedType
    {
        Magic,
        /// <summary>
        /// Param0 MapObject (Target) | Param1 Damage | Param2 Defence | Param3 damageWeapon | Param4 UserMagic | Param5 FinalHit
        /// </summary>
        Damage,
        RangeDamage,        
        Spawn,
        Die,
        Recall,
        MapMovement,
        Mine,
        NPC,
        Poison,
        DamageIndicator,
        Quest,

        // Sanjian
        SpellEffect,

        /// <summary>
        /// 影华连击：特效播完后的延迟位移 | Param0 Point (Destination) | Param1 MirDirection
        /// </summary>
        ShadowComboDash,

        /// <summary>
        /// 拔刀术-秘籍：连砍收招后把身前一排怪推开 | Param0 MirDirection | Param1 int (searchCells) | Param2 int (pushCells)
        /// </summary>
        FlashDashRarePush,

        /// <summary>
        /// 血龙兽-秘籍：等「一次性召唤」全部落地后，统一给身上所有召唤物挂血龙水 | Param0 int (duration 秒)
        /// </summary>
        PetEnhancerRareApply,

        /// <summary>
        /// 时空剑-秘籍：劈砍动作特效播完后补「最后一击」的重击 | Param0 UserMagic | Param1 MapObject (目标) | Param2 int (伤害)
        /// </summary>
        DimensionalSwordFinal,
    }

    public class DelayedAction
    {
        protected static Envir Envir
        {
            get { return Envir.Main; }
        }

        public DelayedType Type;
        public long Time;
        public long StartTime;
        public object[] Params;

        public bool FlaggedToRemove;

        public DelayedAction(DelayedType type, long time, params object[] p)
        {
            StartTime = Envir.Time;
            Type = type;
            Time = time;
            Params = p;
        }
    }
}
