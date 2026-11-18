using System.IO;

namespace ClientPackets;

public sealed class AddFriend : Packet
{
	public string Name;

	public bool Blocked;

	public override short Index => 124;

	protected override void ReadPacket(BinaryReader reader)
	{
		Name = reader.ReadString();
		Blocked = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Name);
		writer.Write(Blocked);
	}
}
