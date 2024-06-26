﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PassivePowers;

[UsedImplicitly]
public static class BossPowerPatches
{
	[HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyRunStaminaDrain))]
	private class ReduceRunStaminaUsage
	{
		private static void Prefix(SEMan __instance, ref float drain)
		{
			if (__instance.m_character is Player)
			{
				drain *= 1 - RunStamina.Total() / 100f;
			}
		}
	}

	[HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyJumpStaminaUsage))]
	private class ReduceJumpStaminaUsage
	{
		private static void Prefix(SEMan __instance, ref float staminaUse)
		{
			if (__instance.m_character is Player)
			{
				staminaUse *= 1 - JumpStamina.Total() / 100f;
			}
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.GetJogSpeedFactor))]
	private class IncreaseJogSpeed
	{
		private static void Postfix(ref float __result)
		{
			__result += MovementSpeed.Total() / 100f;
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.GetRunSpeedFactor))]
	private class IncreaseRunSpeed
	{
		private static void Postfix(ref float __result)
		{
			__result += MovementSpeed.Total() / 100f;
		}
	}

	[HarmonyPatch(typeof(Character), nameof(Character.UpdateSwimming))]
	private class IncreaseSwimSpeed
	{
		private static void Prefix(Character __instance)
		{
			if (__instance is Player player)
			{
				player.m_swimSpeed *= 1 + SwimSpeed.Total() / 100f;
			}
		}

		private static void Postfix(Character __instance)
		{
			if (__instance is Player player)
			{
				player.m_swimSpeed /= 1 + SwimSpeed.Total() / 100f;
			}
		}
	}

	[HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyAttack))]
	private class IncreaseTerrainDamage
	{
		private static void Prefix(SEMan __instance, ref HitData hitData)
		{
			if (__instance.m_character is Player)
			{
				hitData.m_damage.m_chop *= 1 + TreeDamage.Total() / 100f;
				hitData.m_damage.m_pickaxe *= 1 + MiningDamage.Total() / 100f;
			}
		}
	}

	[HarmonyPatch(typeof(Character), nameof(Character.RPC_Damage))]
	private class DecreaseDamageTaken
	{
		private static void Prefix(Character __instance, HitData hit)
		{
			if (__instance is Player)
			{
				hit.m_damage.m_blunt *= 1 - PhysicalDamage.Total() / 100f;
				hit.m_damage.m_pierce *= 1 - PhysicalDamage.Total() / 100f;
				hit.m_damage.m_slash *= 1 - PhysicalDamage.Total() / 100f;

				hit.m_damage.m_fire *= 1 - ElementalDamage.Total() / 100f;
				hit.m_damage.m_frost *= 1 - ElementalDamage.Total() / 100f;
				hit.m_damage.m_poison *= 1 - ElementalDamage.Total() / 100f;
				hit.m_damage.m_lightning *= 1 - ElementalDamage.Total() / 100f;
			}
		}
	}

	[HarmonyPatch(typeof(Character), nameof(Character.Damage))]
	private class AddFireDamage
	{
		private static void Prefix(HitData hit)
		{
			if (hit.GetAttacker() is Player)
			{
				hit.m_damage.m_fire += hit.GetTotalDamage() * BonusFireDamage.Total() / 100f;
			}
		}
	}

	[HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyHealthRegen))]
	public static class IncreaseHealthRegen
	{
		[UsedImplicitly]
		public static void Postfix(SEMan __instance, ref float regenMultiplier)
		{
			if (__instance.m_character is Player)
			{
				regenMultiplier += HealthRegen.Total() / 100f;
			}
		}
	}

	private static float ShipValue(Ship ship, Type type)
	{
		float active = ((IConvertible)type.GetMethod("Total", BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy)!.Invoke(null, Array.Empty<object>())).ToSingle(CultureInfo.InvariantCulture) / 100;
		if (active > 0)
		{
			return active;
		}

		float passive = 0;
		foreach (KeyValuePair<string, BossConfig> kv in PassivePowers.activeBossConfigs)
		{
			foreach (PowerConfig cfg in kv.Value.Configs)
			{
				if (cfg.GetType() == type)
				{
					List<Player> playersWithPower = ship.m_players.FindAll(p => Utils.getPassivePowers(p.m_nview.GetZDO()?.GetString("PassivePowers GuardianPowers") ?? "").Contains(kv.Key));
					passive += ((IConvertible)cfg.BoxedPassive).ToSingle(CultureInfo.InvariantCulture) * playersWithPower.Count / Mathf.Max(1, ship.m_players.Count);
				}
			}
		}
		return passive;
	}

	[HarmonyPatch(typeof(Ship), nameof(Ship.IsWindControllActive))]
	private class TurnWindInFavor
	{
		private static void Postfix(Ship __instance, ref bool __result)
		{
			float chance = ShipValue(__instance, typeof(TailWind));

			Random.State state = Random.state;
			Random.InitState((int)(EnvMan.instance.m_totalSeconds / EnvMan.instance.m_windPeriodDuration));
			if (Random.Range(0f, 1f) < chance)
			{
				__result = true;
			}
			Random.state = state;
		}
	}

	[HarmonyPatch(typeof(Ship), nameof(Ship.GetSailForce))]
	private class IncreaseSailingSpeed
	{
		private static float UpdateWindIntensity(float windIntensity, Ship ship)
		{
			float modifier = ShipValue(ship, typeof(WindSpeed));
			if (Vector3.Angle(ship.transform.forward, EnvMan.instance.GetWindDir()) > 90)
			{
				return windIntensity * (1 - modifier);
			}
			return windIntensity * (1 + modifier);
		}

		private static readonly MethodInfo WindIntensityGetter = AccessTools.DeclaredMethod(typeof(EnvMan), nameof(EnvMan.GetWindIntensity));
		private static readonly MethodInfo WindIntensityUpdater = AccessTools.DeclaredMethod(typeof(IncreaseSailingSpeed), nameof(UpdateWindIntensity));

		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			foreach (CodeInstruction instruction in instructions)
			{
				yield return instruction;
				if (instruction.opcode == OpCodes.Callvirt && instruction.OperandIs(WindIntensityGetter))
				{
					yield return new CodeInstruction(OpCodes.Ldarg_0);
					yield return new CodeInstruction(OpCodes.Call, WindIntensityUpdater);
				}
			}
		}
	}

	[HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyEitrRegen))]
	private static class IncreaseEitrRegen
	{
		private static void Prefix(ref float eitrMultiplier)
		{
			eitrMultiplier *= 1 + EitrRegen.Total() / 100f;
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.GetMaxCarryWeight))]
	private static class IncreaseCarryWeight
	{
		[UsedImplicitly]
		private static void Postfix(Player __instance, ref float __result)
		{
			__result *= 1 + CarryWeight.Total() / 100f;
		}
	}
}
