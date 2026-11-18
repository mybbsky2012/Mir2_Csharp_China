using System.IO;

public class IntelligentCreatureRules
{
	public int MinimalFullness = 1;

	public bool MousePickupEnabled;

	public int MousePickupRange;

	public bool AutoPickupEnabled;

	public int AutoPickupRange;

	public bool SemiAutoPickupEnabled;

	public int SemiAutoPickupRange;

	public bool CanProduceBlackStone;

	public IntelligentCreatureRules()
	{
	}

	public IntelligentCreatureRules(BinaryReader reader)
	{
		MinimalFullness = reader.ReadInt32();
		MousePickupEnabled = reader.ReadBoolean();
		MousePickupRange = reader.ReadInt32();
		AutoPickupEnabled = reader.ReadBoolean();
		AutoPickupRange = reader.ReadInt32();
		SemiAutoPickupEnabled = reader.ReadBoolean();
		SemiAutoPickupRange = reader.ReadInt32();
		CanProduceBlackStone = reader.ReadBoolean();
	}

	public void Save(BinaryWriter writer)
	{
		writer.Write(MinimalFullness);
		writer.Write(MousePickupEnabled);
		writer.Write(MousePickupRange);
		writer.Write(AutoPickupEnabled);
		writer.Write(AutoPickupRange);
		writer.Write(SemiAutoPickupEnabled);
		writer.Write(SemiAutoPickupRange);
		writer.Write(CanProduceBlackStone);
	}
}
