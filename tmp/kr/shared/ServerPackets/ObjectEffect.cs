using System.IO;

namespace ServerPackets;

public sealed class ObjectEffect : Packet
{
	public uint ObjectID;

	public SpellEffect Effect;

	public uint EffectType;

	public uint DelayTime;

	public uint Time;

	public override short Index => 122;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Effect = (SpellEffect)reader.ReadByte();
		EffectType = reader.ReadUInt32();
		DelayTime = reader.ReadUInt32();
		Time = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write((byte)Effect);
		writer.Write(EffectType);
		writer.Write(DelayTime);
		writer.Write(Time);
	}
}
