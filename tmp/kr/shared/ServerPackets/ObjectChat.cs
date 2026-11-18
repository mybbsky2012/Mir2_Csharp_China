using System.IO;

namespace ServerPackets;

public sealed class ObjectChat : Packet
{
	public uint ObjectID;

	public string Text = string.Empty;

	public ChatType Type;

	public override short Index => 31;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Text = reader.ReadString();
		Type = (ChatType)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Text);
		writer.Write((byte)Type);
	}
}
