using System.IO;

namespace ServerPackets;

public sealed class GroupInvite : Packet
{
	public string Name = string.Empty;

	public override short Index => 132;

	protected override void ReadPacket(BinaryReader reader)
	{
		Name = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Name);
	}
}
