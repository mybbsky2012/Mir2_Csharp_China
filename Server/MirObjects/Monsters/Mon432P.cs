using System;
using System.Collections.Generic;
using System.Drawing;
using Server.MirDatabase;
using Server.MirEnvir;
using ServerPackets;

namespace Server.MirObjects.Monsters
{

public class Mon432P : EvilCentipede
{
	protected internal Mon432P(MonsterInfo info)
		: base(info)
	{
	}

	protected override bool InAttackRange()
	{
		if (Visible && base.CurrentMap == Target.CurrentMap)
		{
			return Functions.InRange(CurrentLocation, Target.CurrentLocation, Info.ViewRange);
		}
		return false;
	}

	protected override void ProcessTarget()
	{
		if (Target != null && CanAttack && InAttackRange())
		{
			Attack();
			if (Target != null && Target.Dead)
			{
				FindTarget();
			}
		}
	}

	protected override void Attack()
	{
		if (!Target.IsAttackTarget(this))
		{
			Target = null;
			return;
		}
		ShockTime = 0L;
		ActionTime = MapObject.Envir.Time + 600;
		Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
		if (!(CurrentLocation == Target.CurrentLocation) && Functions.InRange(CurrentLocation, Target.CurrentLocation, 1))
		{
			AttackTime = MapObject.Envir.Time + AttackSpeed;
			Broadcast(new ObjectAttack
			{
				ObjectID = ObjectID,
				Direction = Direction,
				Location = CurrentLocation
			});
			int attackPower = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
			if (attackPower != 0)
			{
				DelayedAction item = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 600, Target, attackPower, DefenceType.ACAgility);
				ActionList.Add(item);
			}
			return;
		}
		AttackTime = MapObject.Envir.Time + AttackSpeed + 600;
		Broadcast(new ObjectRangeAttack
		{
			ObjectID = ObjectID,
			Direction = Direction,
			Location = CurrentLocation,
			TargetID = Target.ObjectID
		});
		int attackPower2 = GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]);
		if (attackPower2 != 0)
		{
			int num = Functions.MaxDistance(CurrentLocation, Target.CurrentLocation) * 50 + 600;
			DelayedAction item2 = new DelayedAction(DelayedType.RangeDamage, MapObject.Envir.Time + num, Target, attackPower2, DefenceType.MACAgility);
			ActionList.Add(item2);
		}
	}

	protected override void CompleteAttack(IList<object> data)
	{
		MapObject mapObject = (MapObject)data[0];
		int damage = (int)data[1];
		DefenceType type = (DefenceType)data[2];
		if (mapObject != null && mapObject.IsAttackTarget(this) && mapObject.CurrentMap == base.CurrentMap && mapObject.Node != null)
		{
			mapObject.Attacked(this, damage, type);
		}
	}

	protected override void CompleteRangeAttack(IList<object> data)
	{
		MapObject mapObject = (MapObject)data[0];
		int damage = (int)data[1];
		DefenceType type = (DefenceType)data[2];
		if (mapObject != null && mapObject.IsAttackTarget(this) && mapObject.CurrentMap == base.CurrentMap && mapObject.Node != null && mapObject.Attacked(this, damage, type) > 0)
		{
			switch (Info.Effect)
			{
			case 0:
				PoisonTarget(mapObject, 7, 5L, PoisonType.Green, 1000L);
				break;
			case 1:
				PoisonTarget(mapObject, 7, 5L, PoisonType.Frozen, 1000L);
				break;
			}
		}
	}
}
}
