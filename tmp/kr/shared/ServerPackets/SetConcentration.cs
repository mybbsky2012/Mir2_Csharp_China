using System.IO;

namespace ServerPackets;

public sealed class SetConcentration : Packet
{
	public uint ObjectID;

	public bool Enabled;

	public bool Interrupted;

	public override short Index => 214;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Enabled = reader.ReadBoolean();
		Interrupted = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Enabled);
		writer.Write(Interrupted);
	}
}
