using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace ServerPackets;

public sealed class ObjectMonster : Packet
{
	public uint ObjectID;

	public string Name = string.Empty;

	public Color NameColour;

	public Point Location;

	public Monster Image;

	public ushort AI;

	public MirDirection Direction;

	public byte Effect;

	public byte Light;

	public bool Dead;

	public bool Skeleton;

	public PoisonType Poison;

	public bool Hidden;

	public bool Extra;

	public byte ExtraByte;

	public long ShockTime;

	public bool BindingShotCenter;

	public bool NoWalkThrough;

	public List<BuffType> Buffs = new List<BuffType>();

	public override short Index => 69;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Name = reader.ReadString();
		NameColour = Color.FromArgb(reader.ReadInt32());
		Location = new Point(reader.ReadInt32(), reader.ReadInt32());
		Image = (Monster)reader.ReadUInt16();
		Direction = (MirDirection)reader.ReadByte();
		Effect = reader.ReadByte();
		AI = reader.ReadUInt16();
		Light = reader.ReadByte();
		Dead = reader.ReadBoolean();
		Skeleton = reader.ReadBoolean();
		Poison = (PoisonType)reader.ReadUInt16();
		Hidden = reader.ReadBoolean();
		ShockTime = reader.ReadInt64();
		BindingShotCenter = reader.ReadBoolean();
		Extra = reader.ReadBoolean();
		ExtraByte = reader.ReadByte();
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			Buffs.Add((BuffType)reader.ReadByte());
		}
		NoWalkThrough = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Name);
		writer.Write(NameColour.ToArgb());
		writer.Write(Location.X);
		writer.Write(Location.Y);
		writer.Write((ushort)Image);
		writer.Write((byte)Direction);
		writer.Write(Effect);
		writer.Write(AI);
		writer.Write(Light);
		writer.Write(Dead);
		writer.Write(Skeleton);
		writer.Write((ushort)Poison);
		writer.Write(Hidden);
		writer.Write(ShockTime);
		writer.Write(BindingShotCenter);
		writer.Write(Extra);
		writer.Write(ExtraByte);
		writer.Write(Buffs.Count);
		for (int i = 0; i < Buffs.Count; i++)
		{
			writer.Write((byte)Buffs[i]);
		}
		writer.Write(NoWalkThrough);
	}
}
