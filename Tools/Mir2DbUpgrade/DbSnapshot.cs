using System.Text;
using Server.MirDatabase;
using Server.MirEnvir;

namespace Mir2DbUpgrade;

/// <summary>
/// 数据库内容快照。把整个库的关键数据导成「一行一条」的文本，用于升级前后逐条比对。
/// 覆盖：地图与刷怪点、物品表、怪物表、NPC、账号、角色（属性/背包/装备/任务栏/技能/BUFF/邮件）、英雄、拍卖。
/// 只保留能代表数据本身的内容（UniqueID / Index / 数量 / 持久 / 内嵌物品），不带任何运行时对象。
/// </summary>
public static class DbSnapshot
{
    public static List<string> Snapshot(Envir e)
    {
        var sb = new List<string>();
        var seen = new HashSet<string>();
        int dupAccount = 0;

        sb.Add($"VERSION|{Envir.LoadVersion}");
        sb.Add($"MAPS|{e.MapInfoList.Count}");
        sb.Add($"ITEMS|{e.ItemInfoList.Count}");
        sb.Add($"MONSTERS|{e.MonsterInfoList.Count}");
        sb.Add($"NPCS|{e.NPCInfoList.Count}");
        sb.Add($"QUESTS|{e.QuestInfoList.Count}");
        sb.Add($"MAGICS|{e.MagicInfoList.Count}");
        sb.Add($"SHOPS|{e.GameShopList.Count}");
        sb.Add($"CONQUESTS|{e.ConquestInfoList.Count}");
        sb.Add($"ACCOUNTS|{e.AccountList.Count}");
        sb.Add($"CHARACTERS|{e.CharacterList.Count}");
        sb.Add($"HEROES|{e.HeroList.Count}");
        sb.Add($"AUCTIONS|{e.Auctions.Count}");
        sb.Add($"IDS|acc={e.NextAccountID}|chr={e.NextCharacterID}|hero={e.NextHeroID}|item={e.NextUserItemID}|mail={e.NextMailID}");

        foreach (var mi in e.MapInfoList)
        {
            sb.Add($"MAP|{mi?.Index}|{mi?.FileName}|respawns={mi?.Respawns?.Count ?? -1}|npcs={mi?.NPCs?.Count ?? -1}");
            if (mi?.Respawns == null) continue;
            foreach (var r in mi.Respawns)
                sb.Add($"RESP|{mi.Index}|{r?.MonsterIndex}|{r?.Count}|{r?.Location.X},{r?.Location.Y}|{r?.Spread}");
        }

        foreach (var ii in e.ItemInfoList)
            sb.Add($"ITEMINFO|{ii?.Index}|{ii?.Name}");

        foreach (var mi in e.MonsterInfoList)
            sb.Add($"MON|{mi?.Index}|{mi?.Name}");

        foreach (var n in e.NPCInfoList)
            sb.Add($"NPC|{n?.Index}|{n?.Name}|{n?.FileName}");

        foreach (var a in e.AccountList.Where(a => a != null).OrderBy(a => a.Index))
        {
            if (!seen.Add(a.AccountID)) dupAccount++;
            sb.Add($"ACC|{a.Index}|{a.AccountID}|gold={a.Gold}|credit={a.Credit}|admin={a.AdminAccount}|chars={a.Characters.Count}");

            foreach (var c in a.Characters.Where(c => c != null).OrderBy(c => c.Index))
                AddCharacter(sb, a.AccountID, c);
        }
        if (dupAccount > 0) sb.Add($"DUPACCOUNT|{dupAccount}");

        foreach (var h in e.HeroList.Where(h => h != null).OrderBy(h => h.Index))
        {
            sb.Add($"HERO|{h.Index}|{h.Name}|{CharLine(h)}|seal={h.SealCount}|grade={h.Grade}|autopot={h.AutoPot}");
            AddContainer(sb, $"HERO|{h.Name}", "bag", h.Inventory);
            AddContainer(sb, $"HERO|{h.Name}", "eq", h.Equipment);
            foreach (var b in h.Buffs) sb.Add($"HERO|{h.Name}|BUFF|{BuffSig(b)}");
            foreach (var m in h.Magics) sb.Add($"HERO|{h.Name}|MAGIC|{m?.Spell}|{m?.Level}");
        }

        foreach (var a in e.Auctions.OrderBy(a => a.AuctionID))
            sb.Add($"AUCTION|{a.AuctionID}|{a.SellerIndex}|{ItemSig(a.Item, 0)}");

        return sb;
    }

    private static void AddCharacter(List<string> sb, string accountId, CharacterInfo c)
    {
        sb.Add($"CHR|{accountId}|{c.Index}|{c.Name}|{CharLine(c)}");
        AddContainer(sb, $"CHR|{accountId}|{c.Name}", "bag", c.Inventory);
        AddContainer(sb, $"CHR|{accountId}|{c.Name}", "eq", c.Equipment);
        AddContainer(sb, $"CHR|{accountId}|{c.Name}", "quest", c.QuestInventory);

        foreach (var m in c.Magics)
            sb.Add($"CHR|{accountId}|{c.Name}|MAGIC|{m?.Spell}|{m?.Level}|{m?.Experience}");

        foreach (var b in c.Buffs)
            sb.Add($"CHR|{accountId}|{c.Name}|BUFF|{BuffSig(b)}");

        foreach (var mail in c.Mail)
        {
            sb.Add($"CHR|{accountId}|{c.Name}|MAIL|{mail.MailID}|{mail.Sender}|gold={mail.Gold}|locked={mail.Locked}"
                 + $"|collected={mail.Collected}|opened={mail.Opened}|items={mail.Items.Count}");
            for (int i = 0; i < mail.Items.Count; i++)
                sb.Add($"CHR|{accountId}|{c.Name}|MAILITEM|{mail.MailID}|{i}|{ItemSig(mail.Items[i], 0)}");
        }
    }

    private static string CharLine(CharacterInfo c)
    {
        return $"lv={c.Level}|cls={c.Class}|gender={c.Gender}|exp={c.Experience}|hp={c.HP}|mp={c.MP}"
             + $"|map={c.CurrentMapIndex}|loc={c.CurrentLocation.X},{c.CurrentLocation.Y}|pk={c.PKPoints}"
             + $"|bag={CountItems(c.Inventory)}|eq={CountItems(c.Equipment)}|quest={CountItems(c.QuestInventory)}"
             + $"|magics={c.Magics.Count}|buffs={c.Buffs.Count}|mail={c.Mail.Count}";
    }

    private static int CountItems(UserItem[] arr) => arr == null ? 0 : arr.Count(i => i != null);

    private static void AddContainer(List<string> sb, string owner, string slotName, UserItem[] arr)
    {
        if (arr == null) return;
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] == null) continue;
            sb.Add($"{owner}|{slotName}|{i}|{ItemSig(arr[i], 0)}");
        }
    }

    private static string ItemSig(UserItem it, int depth)
    {
        if (it == null) return "<null>";
        if (depth > 4) return $"{it.UniqueID}:{it.ItemIndex}:<deep>";

        var sb = new StringBuilder();
        sb.Append($"{it.UniqueID}:{it.ItemIndex}:x{it.Count}:{it.CurrentDura}/{it.MaxDura}:soul={it.SoulBoundId}:curse={it.Cursed}");

        if (it.Slots != null && it.Slots.Length > 0)
        {
            sb.Append(":slots[");
            for (int i = 0; i < it.Slots.Length; i++)
            {
                if (it.Slots[i] == null) continue;
                sb.Append(i).Append('=').Append(ItemSig(it.Slots[i], depth + 1)).Append(';');
            }
            sb.Append(']');
        }
        return sb.ToString();
    }

    private static string BuffSig(Buff b)
    {
        if (b == null) return "<null>";
        if (b.Info == null) return $"{b.Type}:<noinfo>";
        return $"{b.Type}|{b.Info.Name}|{b.ExpireTime}|{b.Values?.Length ?? -1}|{b.Paused}";
    }
}
