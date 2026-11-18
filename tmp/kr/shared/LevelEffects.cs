using System;

[Flags]
public enum LevelEffects : ushort
{
	None = 0,
	Mist = 1,
	RedDragon = 2,
	BlueDragon = 4,
	Rebirth1 = 8,
	Rebirth2 = 0x10,
	Rebirth3 = 0x20,
	NewBlue = 0x40,
	YellowDragon = 0x80,
	Phoenix = 0x100
}
