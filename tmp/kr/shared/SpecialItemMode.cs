using System;

[Flags]
public enum SpecialItemMode : short
{
	None = 0,
	Paralize = 1,
	Teleport = 2,
	ClearRing = 4,
	Protection = 8,
	Revival = 0x10,
	Muscle = 0x20,
	Flame = 0x40,
	Healing = 0x80,
	Probe = 0x100,
	Skill = 0x200,
	NoDuraLoss = 0x400,
	Blink = 0x800
}
