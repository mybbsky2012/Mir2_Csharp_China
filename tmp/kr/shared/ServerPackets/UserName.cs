using System.IO;

namespace ServerPackets;

public sealed class UserName : Packet
{
	public uint Id;

	public string Name;

	public override short Index => 162;

	protected override void ReadPacket(BinaryReader reader)
	{
		Id = reader.ReadUInt32();
		Name = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Id);
		writer.Write(Name);
	}
}
