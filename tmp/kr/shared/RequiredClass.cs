using System;

[Flags]
public enum RequiredClass : byte
{
	战士 = 1,
	法师 = 2,
	道士 = 4,
	刺客 = 8,
	弓箭 = 0x10,
	战士刺客 = 9,
	战法道 = 7,
	全职业 = 0x1F
}
