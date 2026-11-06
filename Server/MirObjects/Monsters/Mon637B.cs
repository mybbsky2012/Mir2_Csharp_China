using System;
using System.Collections.Generic;
using System.Drawing;
using Server.MirDatabase;
using Server.MirEnvir;
using ServerPackets;

namespace Server.MirObjects.Monsters
{

public class Mon637B : MonsterObject
{
	public bool Visible;

	public long VisibleTime;

	private readonly MonsterInfo Mon637BMob = MapObject.Envir.GetMonsterInfo(Settings.Mon637BMob);

	protected override bool CanMove => false;

	protected override bool CanAttack
	{
		get
		{
			if (Visible)
			{
				return base.CanAttack;
			}
			return false;
		}
	}

	public override bool Blocking
	{
		get
		{
			if (Visible)
			{
				return base.Blocking;
			}
			return false;
		}
	}

	protected internal Mon637B(MonsterInfo info)
		: base(info)
	{
		Visible = false;
		VisibleTime = 0L;
	}

	public override bool Walk(MirDirection dir)
	{
		return false;
	}

	public override bool IsAttackTarget(MonsterObject attacker)
	{
		if (Visible)
		{
			return base.IsAttackTarget(attacker);
		}
		return false;
	}

	public override bool IsAttackTarget(HumanObject attacker)
	{
		if (Visible)
		{
			return base.IsAttackTarget(attacker);
		}
		return false;
	}

	protected override void ProcessRoam()
	{
	}

	protected override void ProcessSearch()
	{
		if (Visible)
		{
			base.ProcessSearch();
		}
	}

	public override Packet GetInfo()
	{
		if (!Visible)
		{
			return null;
		}
		return base.GetInfo();
	}

	protected override void ProcessAI()
	{
		if (!Visible)
		{
			SetHP(Stats[Stat.HP]);
		}
		if (!Dead && MapObject.Envir.Time > VisibleTime)
		{
			VisibleTime = MapObject.Envir.Time + 2000;
			bool flag = FindNearby(Visible ? 7 : 3);
			if (!Visible && flag)
			{
				Visible = true;
				CellTime = MapObject.Envir.Time + 900;
				Broadcast(GetInfo());
				Broadcast(new ObjectShow
				{
					ObjectID = ObjectID
				});
				ActionTime = MapObject.Envir.Time + 2000;
			}
			else if (Visible && !flag)
			{
				Visible = false;
				VisibleTime = MapObject.Envir.Time + 3000;
				Target = null;
				Broadcast(new ObjectHide
				{
					ObjectID = ObjectID
				});
				SetHP(Stats[Stat.HP]);
			}
		}
		base.ProcessAI();
	}

	public override void Turn(MirDirection dir)
	{
		if (Visible && Target != null && !Target.Dead && Target.CurrentMap == base.CurrentMap)
		{
			Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
		}
	}

	protected override void ProcessTarget()
	{
		if (Visible && !Dead && Target != null && CanAttack)
		{
			if (Target.Dead || Target.CurrentMap != base.CurrentMap || !Target.IsAttackTarget(this))
			{
				Target = null;
				return;
			}
			if (!InAttackRange())
			{
				Target = null;
				return;
			}
			Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
			Attack();
		}
	}

	protected override bool InAttackRange()
	{
		if (Target != null && !Target.Dead && Target.CurrentMap == base.CurrentMap)
		{
			return Functions.InRange(CurrentLocation, Target.CurrentLocation, Info.ViewRange);
		}
		return false;
	}

	protected override void Attack()
	{
		if (!Visible || Dead || Target == null)
		{
			return;
		}
		if (Target.Dead || Target.CurrentMap != base.CurrentMap || !Target.IsAttackTarget(this))
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
			ActionTime = MapObject.Envir.Time + 600;
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
					if (attackPower2 != 0 && (MapObject.Envir.Random.Next(100) >= 80 || !HalfmoonAttack(attackPower2, 1200, DefenceType.AC)))
					{
						DelayedAction item2 = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 1200, Target, attackPower2, DefenceType.AC, false);
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
						Type = 1
					});
					int attackPower = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
					if (attackPower != 0)
					{
						if (MapObject.Envir.Random.Next(100) < 80 && HalfmoonAttack(attackPower, 1200))
						{
							PoisonTarget(Target, 5, 5L, PoisonType.Frozen, 1000L, noResist: false, ignoreDefence: false);
							break;
						}
						DelayedAction item = new DelayedAction(DelayedType.Damage, MapObject.Envir.Time + 1200, Target, attackPower, DefenceType.AC, false);
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
				int attackPower5 = GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]);
				if (attackPower5 > 0)
				{
					int num3 = Functions.MaxDistance(CurrentLocation, Target.CurrentLocation) * 60 + 1200;
					DelayedAction item4 = new DelayedAction(DelayedType.RangeDamage, MapObject.Envir.Time + num3, Target, attackPower5, DefenceType.MACAgility, false);
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
				int attackPower4 = GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]);
				if (attackPower4 > 0)
				{
					TargetAreaAttack(Spell.Mon637BAreaBall, attackPower4, 3, 500, 3000, 1500, DefenceType.MAC);
					int num2 = Functions.MaxDistance(CurrentLocation, Target.CurrentLocation) * 60 + 1200;
					DelayedAction item3 = new DelayedAction(DelayedType.RangeDamage, MapObject.Envir.Time + num2, Target, attackPower4, DefenceType.MACAgility, false);
					ActionList.Add(item3);
				}
				break;
			}
			case 2:
				if (MapObject.Envir.Random.Next(100) < 30)
				{
					Broadcast(new ObjectAttack
					{
						ObjectID = ObjectID,
						Direction = Direction,
						Location = CurrentLocation,
						Type = 1
					});
					int attackPower3 = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
					if (attackPower3 != 0 && MapObject.Envir.Random.Next(100) < 80 && SinglePullAttack(attackPower3, DefenceType.AC, 1200, 7, levelLimit: false) && MapObject.Envir.Random.Next(100) < 80 && SpawnSlaves(Mon637BMob, 3, maintainCount: true))
					{
						PoisonTarget(Target, 1, 5L, PoisonType.Frozen, 1000L, noResist: false, ignoreDefence: false);
					}
				}
				break;
			}
		}
	}

	public override void Die()
	{
		foreach (MonsterObject slave in SlaveList)
		{
			slave.Die();
		}
		base.Die();
	}
}
}
