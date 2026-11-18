using System;
using System.IO;

namespace ClientPackets;

public sealed class SpellToggle : Packet
{
	public Spell Spell;

	public SpellToggleState canUse = SpellToggleState.None;

	public override short Index => 66;

	public bool CanUse
	{
		get
		{
			return Convert.ToBoolean(canUse);
		}
		set
		{
			canUse = (SpellToggleState)Convert.ToSByte(value);
		}
	}

	protected override void ReadPacket(BinaryReader reader)
	{
		Spell = (Spell)reader.ReadByte();
		canUse = (SpellToggleState)reader.ReadSByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Spell);
		writer.Write((sbyte)canUse);
	}
}
