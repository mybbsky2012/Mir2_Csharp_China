using System;

[Flags]
public enum GMOptions : byte
{
	None = 0,
	GameMaster = 1,
	Observer = 2,
	Superman = 4
}
