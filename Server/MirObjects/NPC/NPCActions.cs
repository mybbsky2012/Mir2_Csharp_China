namespace Server.MirObjects
{
    public class NPCActions
    {
        public ActionType Type;
        public List<string> Params = new List<string>();

        public NPCActions(ActionType action, params string[] p)
        {
            Type = action;
            Params.AddRange(p);
        }
    }

    public enum ActionType
    {
        Move,
        InstanceMove,
        GiveGold,
        TakeGold,
        GiveGuildGold,
        TakeGuildGold,
        GiveCredit,
        TakeCredit,
        GiveItem,
        TakeItem,
        EquipItem,
        HeroEquipItem,
        GiveExp,
        GiveHeroExp,
        GivePet,
        ClearPets,
        AddNameList,
        DelNameList,
        ClearNameList,
        GiveHP,
        GiveMP,
        ChangeLevel,
        SetPkPoint,
        ReducePkPoint,
        IncreasePkPoint,
        ChangeGender,
        ChangeClass,
        LocalMessage,
        Goto,
        Call,
        GiveSkill,
        RemoveSkill,
        Set,
        Param1,
        Param2,
        Param3,
        Mongen,
        TimeRecall,
        TimeRecallGroup,
        BreakTimeRecall,
        MonClear,
        GroupRecall,
        GroupTeleport,
        DelayGoto,
        Mov,
        Calc,
        GiveBuff,
        RemoveBuff,
        HumUpUp,        //★登仙（11-10）：羽化升重 HUMUPUP [重数=1]
        HumUpDown,      //★登仙（11-10）：羽化降重 HUMUPDOWN [重数=1]
        HumUpReset,     //★登仙（11-10）：完全退回 HUMUPRESET
        AddToGuild,
        RemoveFromGuild,
        RefreshEffects,
        ChangeHair,
        CanGainExp,
        ComposeMail,
        AddMailItem,
        AddMailGold,
        SendMail,
        GroupGoto,
        EnterMap,
        GivePearls,
        TakePearls,
        MakeWeddingRing,
        ForceDivorce,
        GlobalMessage,
        LoadValue,
        SaveValue,
        RemovePet,
        ConquestGuard,
        ConquestGate,
        ConquestWall,
        ConquestSiege,
        TakeConquestGold,
        SetConquestRate,
        StartConquest,
        ScheduleConquest,
        OpenGate,
        CloseGate,
        Break,
        AddGuildNameList,
        DelGuildNameList,
        ClearGuildNameList,
        OpenBrowser,
        GetRandomText,
        PlaySound,
        SetTimer,
        ExpireTimer,
        SetLoopTimer,
        UnequipItem,
        RollDie,
        RollYut,
        Drop,
        ReviveHero,
        SealHero,
        DeleteHero,
		ConquestRepairAll,
		GiveGuildExp,
        Recycle,
        RecycleKey,
        Repair,
        HeroRepair,
        OfflineAFK,
        HeroExp,        //★英雄（11-20）：调整英雄经验 HEROEXP n（正=加，负=扣，只扣当前经验条不掉级）
        HeroSetLevel,   //★英雄（11-20）：直接设定英雄等级 SETHEROLEVEL n
        HeroHumUpUp,    //★英雄（11-20）：英雄羽化升重 HEROHUMUP [n=1]（带参=直接设为 n 重）
        HeroHumUpDown,  //★英雄（11-20）：英雄羽化降重 HEROHUMDOWN [n=1]（降到 0 = 完全退回）
        HeroHumUpReset, //★英雄（11-20）：英雄还原未登仙 RESETHEROHUMUP [1=自动脱羽化装]
        GiveW,          //★11-23：自动穿戴 GIVEW [槽位|类型]（本体从背包穿，无参=穿全部能穿的）
        GiveWHero,      //★11-23：自动穿戴 GIVEWHERO [槽位|类型]（英雄版：从英雄背包穿到英雄身上）
        TakeW,          //★11-23：自动脱下 TAKEW [槽位|类型]（本体脱回背包，无参=全身脱光；背包空格不足=整单拒绝）
        TakeWHero       //★11-23：自动脱下 TAKEWHERO [槽位|类型]（英雄版：英雄脱回英雄背包）
    }
}