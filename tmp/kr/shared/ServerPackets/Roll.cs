using System.IO;

namespace ServerPackets;

public sealed class Roll : Packet
{
	public int Type;

	public string Page;

	public int Result;

	public bool AutoRoll;

	public override short Index => 270;

	protected override void ReadPacket(BinaryReader reader)
	{
		Type = reader.ReadInt32();
		Page = reader.ReadString();
		Result = reader.ReadInt32();
		AutoRoll = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Type);
		writer.Write(Page);
		writer.Write(Result);
		writer.Write(AutoRoll);
	}
}
