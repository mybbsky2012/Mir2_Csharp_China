using System;
using System.Collections.Generic;
using System.Drawing;
using Server.MirDatabase;
using Server.MirEnvir;
using ServerPackets;

namespace Server.MirObjects.Monsters
{

public class Mon622N : MonsterObject
{
	protected override bool CanMove => false;

	protected override bool CanRegen => false;

	protected internal Mon622N(MonsterInfo info)
		: base(info)
	{
	}

	protected override bool InAttackRange()
	{
		if (Target == null)
		{
			return false;
		}
		if (Target.CurrentMap != base.CurrentMap)
		{
			return false;
		}
		return Functions.InRange(CurrentLocation, Target.CurrentLocation, Info.ViewRange);
	}

	protected override void Attack()
	{
		if (Target == null || !Target.IsAttackTarget(this))
		{
			Target = null;
			return;
		}
		ShockTime = 0L;
		ActionTime = MapObject.Envir.Time + 300;
		AttackTime = MapObject.Envir.Time + AttackSpeed;
		Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
		if (Functions.InRange(CurrentLocation, Target.CurrentLocation, 3))
		{
			switch (MapObject.Envir.Random.Next(2))
			{
			case 0:
			{
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
					WideLineAttack(attackPower3, 3, 500, DefenceType.AC);
					DelayedAction item3 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 300, Target, attackPower3, DefenceType.AC, false);
					ActionList.Add(item3);
				}
				break;
			}
			case 1:
				if (MapObject.Envir.Random.Next(100) < 33)
				{
					Broadcast(new ObjectAttack
					{
						ObjectID = ObjectID,
						Direction = Direction,
						Location = CurrentLocation,
						Type = 2
					});
					int attackPower = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC] * 2);
					if (attackPower != 0)
					{
						WideLineAttack(attackPower, 5, 300);
						DelayedAction item = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 300, Target, attackPower, DefenceType.ACAgility, false);
						ActionList.Add(item);
					}
				}
				else
				{
					Broadcast(new ObjectAttack
					{
						ObjectID = ObjectID,
						Direction = Direction,
						Location = CurrentLocation,
						Type = 1
					});
					int attackPower2 = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
					if (attackPower2 != 0)
					{
						WideLineAttack(attackPower2, 4, 300);
						DelayedAction item2 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 300, Target, attackPower2, DefenceType.ACAgility, false);
						ActionList.Add(item2);
					}
				}
				break;
			}
		}
		else
		{
			Broadcast(new ObjectAttack
			{
				ObjectID = ObjectID,
				Direction = Direction,
				Location = CurrentLocation,
				Type = 0
			});
			int attackPower4 = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
			if (attackPower4 != 0)
			{
				Broadcast(new ObjectEffect
				{
					ObjectID = Target.ObjectID,
					Effect = SpellEffect.Mon622NSpikes
				});
				DelayedAction item4 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 300, Target, attackPower4, DefenceType.ACAgility, false);
				ActionList.Add(item4);
			}
		}
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
}
}
