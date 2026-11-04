using System;
using System.Collections.Generic;
using System.Drawing;
using Server.MirDatabase;
using Server.MirEnvir;
using ServerPackets;

namespace Server.MirObjects.Monsters
{

public class Mon612N : MonsterObject
{
	protected internal Mon612N(MonsterInfo info)
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
			bool num = CurrentLocation == Target.CurrentLocation || !Functions.InRange(CurrentLocation, Target.CurrentLocation, 1);
			ActionTime = MapObject.Envir.Time + 300;
			AttackTime = MapObject.Envir.Time + AttackSpeed;
			if (!num)
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
						Type = 0
					});
					int attackPower2 = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
					if (attackPower2 != 0)
					{
						DelayedAction item2 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 600, Target, attackPower2, DefenceType.AC, false);
						ActionList.Add(item2);
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
						Type = 0
					});
					int attackPower = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC] * 2);
					if (attackPower != 0)
					{
						DelayedAction item = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 600, Target, attackPower, DefenceType.ACAgility, false);
						ActionList.Add(item);
					}
					break;
				}
				}
				return;
			}
			switch (MapObject.Envir.Random.Next(3))
			{
			case 0:
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
					DelayedAction item4 = new DelayedAction(DelayedType.RangeDamage, MapObject.Envir.Time + 900, Target, attackPower4, DefenceType.AC, false);
					ActionList.Add(item4);
				}
				break;
			}
			case 1:
			{
				Broadcast(new ObjectRangeAttack
				{
					ObjectID = ObjectID,
					Direction = Direction,
					Location = CurrentLocation,
					TargetID = Target.ObjectID,
					Type = 1
				});
				int attackPower3 = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC] * 2);
				if (attackPower3 != 0)
				{
					DelayedAction item3 = new DelayedAction(DelayedType.RangeDamage, MapObject.Envir.Time + 900, Target, attackPower3, DefenceType.ACAgility, false);
					ActionList.Add(item3);
				}
				break;
			}
			case 2:
				Broadcast(new ObjectRangeAttack
				{
					ObjectID = ObjectID,
					Direction = Direction,
					Location = CurrentLocation,
					TargetID = Target.ObjectID,
					Type = 1
				});
				Mon612NFireWall();
				break;
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
				MoveTo((MapObject.Envir.Random.Next(2) == 0) ? Target.Front : Target.Back);
			}
		}
	}

	private void Mon612NFireWall()
	{
		List<MapObject> list = FindAllTargets(Info.ViewRange, CurrentLocation);
		int count = list.Count;
		if (count == 0)
		{
			return;
		}
		Point currentLocation = list[MapObject.Envir.Random.Next(count)].CurrentLocation;
		for (int i = currentLocation.Y - 1; i <= currentLocation.Y + 1; i++)
		{
			if (i < 0)
			{
				continue;
			}
			if (i >= base.CurrentMap.Height)
			{
				break;
			}
			for (int j = currentLocation.X - 1; j <= currentLocation.X + 1; j++)
			{
				if (j >= 0)
				{
					if (j >= base.CurrentMap.Width)
					{
						break;
					}
					if ((j != CurrentLocation.X || i != CurrentLocation.Y) && base.CurrentMap.GetCell(j, i).Valid)
					{
						int attackPower = GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]);
						int num = 500;
						int num2 = 7000;
						SpellObject spellObject = new SpellObject
						{
							Spell = Spell.Mon612NFlame,
							Value = attackPower,
							ExpireTime = MapObject.Envir.Time + num2 + num,
							TickSpeed = 3000,
							CurrentLocation = new Point(j, i),
							CastLocation = currentLocation,
							Show = (currentLocation.X == j && currentLocation.Y == i),
							CurrentMap = base.CurrentMap,
							Caster = this
						};
						DelayedAction item = new DelayedAction(DelayedType.Spawn, MapObject.Envir.Time + num, spellObject);
						base.CurrentMap.ActionList.Add(item);
					}
				}
			}
		}
	}

	public override void Die()
	{
		base.Die();
	}
}
}
