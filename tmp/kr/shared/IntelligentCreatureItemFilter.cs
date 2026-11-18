using System.IO;

public class IntelligentCreatureItemFilter
{
	public bool PetPickupAll = true;

	public bool PetPickupGold;

	public bool PetPickupWeapons;

	public bool PetPickupArmours;

	public bool PetPickupHelmets;

	public bool PetPickupBoots;

	public bool PetPickupBelts;

	public bool PetPickupAccessories;

	public bool PetPickupOthers;

	public ItemGrade PickupGrade;

	public IntelligentCreatureItemFilter()
	{
	}

	public void SetItemFilter(int idx)
	{
		switch (idx)
		{
		case 0:
			PetPickupAll = true;
			PetPickupGold = false;
			PetPickupWeapons = false;
			PetPickupArmours = false;
			PetPickupHelmets = false;
			PetPickupBoots = false;
			PetPickupBelts = false;
			PetPickupAccessories = false;
			PetPickupOthers = false;
			break;
		case 1:
			PetPickupAll = false;
			PetPickupGold = !PetPickupGold;
			break;
		case 2:
			PetPickupAll = false;
			PetPickupWeapons = !PetPickupWeapons;
			break;
		case 3:
			PetPickupAll = false;
			PetPickupArmours = !PetPickupArmours;
			break;
		case 4:
			PetPickupAll = false;
			PetPickupHelmets = !PetPickupHelmets;
			break;
		case 5:
			PetPickupAll = false;
			PetPickupBoots = !PetPickupBoots;
			break;
		case 6:
			PetPickupAll = false;
			PetPickupBelts = !PetPickupBelts;
			break;
		case 7:
			PetPickupAll = false;
			PetPickupAccessories = !PetPickupAccessories;
			break;
		case 8:
			PetPickupAll = false;
			PetPickupOthers = !PetPickupOthers;
			break;
		}
		if (PetPickupGold && PetPickupWeapons && PetPickupArmours && PetPickupHelmets && PetPickupBoots && PetPickupBelts && PetPickupAccessories && PetPickupOthers)
		{
			PetPickupAll = true;
			PetPickupGold = false;
			PetPickupWeapons = false;
			PetPickupArmours = false;
			PetPickupHelmets = false;
			PetPickupBoots = false;
			PetPickupBelts = false;
			PetPickupAccessories = false;
			PetPickupOthers = false;
		}
		else if (!PetPickupGold && !PetPickupWeapons && !PetPickupArmours && !PetPickupHelmets && !PetPickupBoots && !PetPickupBelts && !PetPickupAccessories && !PetPickupOthers)
		{
			PetPickupAll = true;
		}
	}

	public IntelligentCreatureItemFilter(BinaryReader reader)
	{
		PetPickupAll = reader.ReadBoolean();
		PetPickupGold = reader.ReadBoolean();
		PetPickupWeapons = reader.ReadBoolean();
		PetPickupArmours = reader.ReadBoolean();
		PetPickupHelmets = reader.ReadBoolean();
		PetPickupBoots = reader.ReadBoolean();
		PetPickupBelts = reader.ReadBoolean();
		PetPickupAccessories = reader.ReadBoolean();
		PetPickupOthers = reader.ReadBoolean();
	}

	public void Save(BinaryWriter writer)
	{
		writer.Write(PetPickupAll);
		writer.Write(PetPickupGold);
		writer.Write(PetPickupWeapons);
		writer.Write(PetPickupArmours);
		writer.Write(PetPickupHelmets);
		writer.Write(PetPickupBoots);
		writer.Write(PetPickupBelts);
		writer.Write(PetPickupAccessories);
		writer.Write(PetPickupOthers);
	}
}
