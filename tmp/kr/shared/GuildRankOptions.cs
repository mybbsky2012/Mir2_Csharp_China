using System;

[Flags]
public enum GuildRankOptions : byte
{
	CanChangeRank = 1,
	CanRecruit = 2,
	CanKick = 4,
	CanStoreItem = 8,
	CanRetrieveItem = 0x10,
	CanAlterAlliance = 0x20,
	CanChangeNotice = 0x40,
	CanActivateBuff = 0x80
}
