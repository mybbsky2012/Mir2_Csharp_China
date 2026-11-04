using System;
using System.Collections.Generic;
using System.Drawing;
using Server.MirDatabase;
using Server.MirEnvir;
using ServerPackets;

namespace Server.MirObjects.Monsters
{

public class Mon624B : MonsterObject
{
	protected internal Mon624B(MonsterInfo info)
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
		}
		else
		{
			if (!CanAttack)
			{
				return;
			}
			ShockTime = 0L;
			Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
			bool flag = CurrentLocation == Target.CurrentLocation || !Functions.InRange(CurrentLocation, Target.CurrentLocation, 1);
			bool flag2 = CurrentLocation == Target.CurrentLocation || !Functions.InRange(CurrentLocation, Target.CurrentLocation, 2);
			ActionTime = MapObject.Envir.Time + 500;
			AttackTime = MapObject.Envir.Time + AttackSpeed;
			if (!flag && !flag2)
			{
				switch (MapObject.Envir.Random.Next(5))
				{
				case 0:
				{
					Broadcast(new ObjectAttack
					{
						ObjectID = ObjectID,
						Direction = Direction,
						Location = CurrentLocation,
						Type = 0
					});
					int attackPower2 = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
					if (attackPower2 == 0)
					{
						return;
					}
					DelayedAction item2 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 1200, Target, attackPower2, DefenceType.AC, false);
					ActionList.Add(item2);
					break;
				}
				case 1:
				{
					Broadcast(new ObjectAttack
					{
						ObjectID = ObjectID,
						Direction = Direction,
						Location = CurrentLocation,
						Type = 0
					});
					int attackPower3 = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
					if (attackPower3 == 0)
					{
						return;
					}
					if (MapObject.Envir.Random.Next(100) < 20)
					{
						WideLineAttack(attackPower3, 1, 1200);
					}
					DelayedAction item4 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 1200, Target, attackPower3, DefenceType.AC, false);
					ActionList.Add(item4);
					break;
				}
				case 2:
				{
					Broadcast(new ObjectAttack
					{
						ObjectID = ObjectID,
						Direction = Direction,
						Location = CurrentLocation,
						Type = 0
					});
					int num2 = Stats[Stat.MaxDC];
					if (MapObject.Envir.Random.Next(100) < 20)
					{
						num2 *= 2;
					}
					int attackPower4 = GetAttackPower(Stats[Stat.MinDC], num2);
					if (attackPower4 == 0)
					{
						return;
					}
					DelayedAction item5 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 1200, Target, attackPower4, DefenceType.AC, false);
					ActionList.Add(item5);
					break;
				}
				case 3:
				{
					Broadcast(new ObjectAttack
					{
						ObjectID = ObjectID,
						Direction = Direction,
						Location = CurrentLocation,
						Type = 0
					});
					int num = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
					if (MapObject.Envir.Random.Next(100) < 20)
					{
						num *= 2;
					}
					if (num == 0)
					{
						return;
					}
					DelayedAction item3 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 1200, Target, num, DefenceType.AC, false);
					ActionList.Add(item3);
					break;
				}
				case 4:
				{
					Broadcast(new ObjectAttack
					{
						ObjectID = ObjectID,
						Direction = Direction,
						Location = CurrentLocation,
						Type = 1
					});
					int attackPower = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
					if (attackPower == 0)
					{
						return;
					}
					WideLineAttack(attackPower, 5, 1200);
					DelayedAction item = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 1200, Target, attackPower, DefenceType.ACAgility, false);
					ActionList.Add(item);
					break;
				}
				}
			}
			if (flag)
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
					int attackPower6 = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
					if (attackPower6 == 0)
					{
						return;
					}
					WideLineAttack(attackPower6, 5, 1200);
					PoisonTarget(Target, 7, 7L, PoisonType.Green, 1000L, noResist: true, ignoreDefence: false);
					DelayedAction item7 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 1200, Target, attackPower6, DefenceType.ACAgility, false);
					ActionList.Add(item7);
					break;
				}
				case 1:
				{
					Broadcast(new ObjectAttack
					{
						ObjectID = ObjectID,
						Direction = Direction,
						Location = CurrentLocation,
						Type = 2
					});
					int attackPower5 = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
					if (attackPower5 == 0)
					{
						return;
					}
					WideLineAttack(attackPower5, 5, 1200);
					DelayedAction item6 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 1200, Target, attackPower5, DefenceType.ACAgility, false);
					ActionList.Add(item6);
					break;
				}
				}
			}
			if (!flag2)
			{
				return;
			}
			switch (MapObject.Envir.Random.Next(3))
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
				int attackPower8 = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
				if (attackPower8 != 0)
				{
					WideLineAttack(attackPower8, 5, 1200);
					PoisonTarget(Target, 5, 7L, PoisonType.Green, 1000L, noResist: true, ignoreDefence: false);
					DelayedAction item9 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 1200, Target, attackPower8, DefenceType.ACAgility, false);
					ActionList.Add(item9);
				}
				break;
			}
			case 1:
			{
				Broadcast(new ObjectAttack
				{
					ObjectID = ObjectID,
					Direction = Direction,
					Location = CurrentLocation,
					Type = 2
				});
				int attackPower9 = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
				if (attackPower9 != 0)
				{
					WideLineAttack(attackPower9, 5, 1200);
					DelayedAction item10 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 1200, Target, attackPower9, DefenceType.ACAgility, false);
					ActionList.Add(item10);
				}
				break;
			}
			case 2:
			{
				Broadcast(new ObjectRangeAttack
				{
					ObjectID = ObjectID,
					Direction = Direction,
					Location = CurrentLocation,
					TargetID = Target.ObjectID,
					Type = 0
				});
				int attackPower7 = GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]);
				if (attackPower7 != 0)
				{
					DelayedAction item8 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 1200, Target, attackPower7, DefenceType.MAC, false);
					ActionList.Add(item8);
				}
				break;
			}
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
				MoveTo(Target.Front);
			}
		}
	}

	public override void Die()
	{
		base.Die();
	}
}
}
