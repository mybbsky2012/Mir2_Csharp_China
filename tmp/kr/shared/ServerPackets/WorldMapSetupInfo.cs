using System.IO;

namespace ServerPackets;

public sealed class WorldMapSetupInfo : Packet
{
	public WorldMapSetup Setup;

	public int TeleportToNPCCost;

	public override short Index => 19;

	protected override void ReadPacket(BinaryReader reader)
	{
		Setup = new WorldMapSetup(reader);
		TeleportToNPCCost = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		Setup.Save(writer);
		writer.Write(TeleportToNPCCost);
	}
}
