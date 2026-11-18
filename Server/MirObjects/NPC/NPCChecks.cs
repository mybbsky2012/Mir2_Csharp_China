namespace Server.MirObjects
{
    public class NPCChecks
    {
        public CheckType Type;
        public List<string> Params = new List<string>();

        public NPCChecks(CheckType check, params string[] p)
        {
            Type = check;

            for (int i = 0; i < p.Length; i++)
                Params.Add(p[i]);
        }
    }

    public enum CheckType
    {
        IsAdmin,
        Level,
        HeroLevel,
        CheckItem,
        CheckGold,
        CheckGuildGold,
        CheckCredit,
        CheckGender,
        CheckClass,
        CheckDay,
        CheckHour,
        CheckMinute,
        CheckNameList,
        CheckPkPoint,
        CheckRange,
        Check,
        CheckHum,
        CheckMon,
        CheckExactMon,
        Random,
        Groupleader,
        GroupCount,
        GroupCheckNearby,
        PetLevel,
        PetCount,
        CheckCalc,
        InGuild,
        CheckMap,
        CheckQuest,
        CheckRelationship,
        CheckWeddingRing,
        CheckPet,
        HasBagSpace,
        IsNewHuman,
        CheckConquest,
        AffordGuard,
        AffordGate,
        AffordWall,
        AffordSiege,
        CheckPermission,
        ConquestAvailable,
        ConquestOwner,
        CheckGuildNameList,
        CheckTimer,
        CheckBuff,
        CheckHumUp,     //★登仙（11-10）：检测羽化重数 CHECKHUMUP >= 3
        CheckTransform,
        CheckLoopTimer,
        CheckHero,
        CheckHeroClass,
        CheckHeroGender,
        CheckHeroItem,
        CheckHeroUse,
        CheckSafeZone,
        CheckEmptyEquip    //★羽化登仙任务：装备栏是否全空 CHECKEMPTYEQUIP（第 11 环羽化仪式用）
    }
}