using System;
using System.Collections.Generic;
using System.Drawing;
using Server.MirDatabase;
using Server.MirEnvir;
using ServerPackets;

namespace Server.MirObjects.Monsters
{

public class Mon614B : MonsterObject
{
	protected internal Mon614B(MonsterInfo info)
		: base(info)
	{
	}

	protected override bool InAttackRange()
	{
		if (Target.CurrentMap != base.CurrentMap)
		{
			return false;
		}
		if (base.CurrentMap == Target.CurrentMap)
		{
			return Functions.InRange(CurrentLocation, Target.CurrentLocation, Info.ViewRange);
		}
		return false;
	}

	protected override void Attack()
	{
		if (!Target.IsAttackTarget(this))
		{
			Target = null;
			return;
		}
		ActionTime = MapObject.Envir.Time + 300;
		AttackTime = MapObject.Envir.Time + AttackSpeed;
		ShockTime = 0L;
		Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
		if (!(CurrentLocation == Target.CurrentLocation) && Functions.InRange(CurrentLocation, Target.CurrentLocation, 1))
		{
			switch (MapObject.Envir.Random.Next(3))
			{
			case 0:
			{
				DefenceType defenceType2 = ((MapObject.Envir.Random.Next(2) == 0) ? DefenceType.AC : DefenceType.ACAgility);
				Broadcast(new ObjectAttack
				{
					ObjectID = ObjectID,
					Direction = Direction,
					Location = CurrentLocation,
					Type = 0
				});
				int attackPower2 = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
				if (attackPower2 != 0)
				{
					DelayedAction item2 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 600, Target, attackPower2, defenceType2, false);
					ActionList.Add(item2);
				}
				break;
			}
			case 1:
			{
				DefenceType defenceType3 = ((MapObject.Envir.Random.Next(2) == 0) ? DefenceType.AC : DefenceType.ACAgility);
				MapObject.Envir.Random.Next(2);
				Broadcast(new ObjectAttack
				{
					ObjectID = ObjectID,
					Direction = Direction,
					Location = CurrentLocation,
					Type = 1
				});
				int attackPower3 = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
				if (attackPower3 != 0)
				{
					DelayedAction item3 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 800, Target, attackPower3, defenceType3, false);
					ActionList.Add(item3);
				}
				break;
			}
			case 2:
			{
				DefenceType defenceType = ((MapObject.Envir.Random.Next(2) == 0) ? DefenceType.AC : DefenceType.ACAgility);
				Broadcast(new ObjectAttack
				{
					ObjectID = ObjectID,
					Direction = Direction,
					Location = CurrentLocation,
					Type = 2
				});
				int attackPower = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
				if (attackPower != 0)
				{
					DelayedAction item = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 600, Target, attackPower, defenceType, false);
					ActionList.Add(item);
				}
				break;
			}
			}
		}
		else if (MapObject.Envir.Random.Next(8) != 7)
		{
			Broadcast(new ObjectRangeAttack
			{
				ObjectID = ObjectID,
				Direction = Direction,
				Location = CurrentLocation,
				TargetID = Target.ObjectID,
				Type = 0
			});
			int attackPower4 = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
			if (attackPower4 != 0)
			{
				DelayedAction item4 = new DelayedAction(DelayedType.RangeDamage, MapObject.Envir.Time + 800, Target, attackPower4, DefenceType.MACAgility, false);
				ActionList.Add(item4);
			}
		}
		else
		{
			Broadcast(new ObjectRangeAttack
			{
				ObjectID = ObjectID,
				Direction = Direction,
				Location = CurrentLocation,
				TargetID = Target.ObjectID,
				Type = 1
			});
			int attackPower5 = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
			if (attackPower5 != 0)
			{
				PoisonTarget(Target, 7, 5L, PoisonType.Green, 1000L);
				DelayedAction item5 = new DelayedAction(DelayedType.RangeDamage, MapObject.Envir.Time + 800, Target, attackPower5, DefenceType.MACAgility, false);
				ActionList.Add(item5);
			}
		}
	}

	protected override void ProcessTarget()
	{
		if (Target != null)
		{
			if (InAttackRange() && CanAttack)
			{
				Attack();
			}
			else if (MapObject.Envir.Time < ShockTime)
			{
				Target = null;
			}
			else
			{
				MoveTo(Target.CurrentLocation);
			}
		}
	}
}
}
