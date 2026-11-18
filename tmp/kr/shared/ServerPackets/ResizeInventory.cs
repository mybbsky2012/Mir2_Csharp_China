using System.IO;

namespace ServerPackets;

public sealed class ResizeInventory : Packet
{
	public int Size;

	public override short Index => 235;

	protected override void ReadPacket(BinaryReader reader)
	{
		Size = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Size);
	}
}
