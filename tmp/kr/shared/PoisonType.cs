using System;

[Flags]
public enum PoisonType : ushort
{
	None = 0,
	Green = 1,
	Red = 2,
	Slow = 4,
	Frozen = 8,
	Stun = 0x10,
	Paralysis = 0x20,
	DelayedExplosion = 0x40,
	Bleeding = 0x80,
	LRParalysis = 0x100,
	Blindness = 0x200,
	Dazed = 0x400
}
