using System.IO;

namespace ServerPackets;

public sealed class ObjectHero : ObjectPlayer
{
	public string OwnerName;

	public override short Index => 25;

	protected override void ReadPacket(BinaryReader reader)
	{
		base.ReadPacket(reader);
		OwnerName = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		base.WritePacket(writer);
		writer.Write(OwnerName);
	}
}
