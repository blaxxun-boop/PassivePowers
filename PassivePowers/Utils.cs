﻿using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace PassivePowers;

public static class Utils
{
	public static bool GetToggle(this ConfigEntry<Toggle> toggle)
	{
		return toggle.Value == Toggle.On;
	}

	public static List<string> getPassivePowers(Player player) => getPassivePowers(player.m_guardianPower);
	public static List<string> getPassivePowers(string powerList) => powerList.IsNullOrWhiteSpace() ? new List<string>() : powerList.Split(',').ToList();

	public static string getHumanFriendlyTime(int seconds)
	{
		TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);

		string secondsText = Localization.instance.Localize(timeSpan.Seconds >= 2 ? "$powers_second_plural" : "$powers_second_singular", timeSpan.Seconds.ToString());
		string minutesText = Localization.instance.Localize(timeSpan.Minutes >= 2 ? "$powers_minute_plural" : "$powers_minute_singular", timeSpan.Minutes.ToString());
		return timeSpan.TotalMinutes >= 1 ? timeSpan.Seconds == 0 ? minutesText : Localization.instance.Localize("$powers_time_bind", minutesText, secondsText) : secondsText;
	}

	public static bool CanApplyPower(Player player, string power) => getPassivePowers(player).Contains(power) || player.GetSEMan().HaveStatusEffect("PassivePowers " + power);
}

public static class Power
{
	public const string Eikthyr = "GP_Eikthyr";
	public const string TheElder = "GP_TheElder";
	public const string Bonemass = "GP_Bonemass";
	public const string Moder = "GP_Moder";
	public const string Yagluth = "GP_Yagluth";
	public const string Queen = "GP_Queen";
}

[HarmonyPatch(typeof(Player), nameof(Player.SetGuardianPower))]
public class Patch_Player_SetGuardianPower
{
	private static bool Prefix(Player __instance, string name)
	{
		if (name == "" || name.Contains(","))
		{
			__instance.m_guardianPower = name;
		}
		else
		{
			List<string> powers = Utils.getPassivePowers(__instance);
			if (!powers.Remove(name))
			{
				powers.Add(name);
			}

			if (powers.Count > PassivePowers.maximumBossPowers.Value)
			{
				powers.RemoveAt(0);
			}

			__instance.m_guardianPower = string.Join(",", powers);
		}

		__instance.m_nview.GetZDO()?.Set("PassivePowers GuardianPowers", __instance.m_guardianPower);

		return false;
	}
}

public class PowerConfig<T>
{
	public string powerName = null!;
	public ConfigEntry<T> active = null!;
	public ConfigEntry<T> passive = null!;

	public T Value => (Player.m_localPlayer?.m_seman.HaveStatusEffect("PassivePowers Depletion " + powerName) != false ? default : Player.m_localPlayer.m_seman.HaveStatusEffect("PassivePowers " + powerName) ? active.Value : passive.Value)!;
}
