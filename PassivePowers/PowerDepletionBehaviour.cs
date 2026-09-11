using UnityEngine;

namespace PassivePowers;

public class PowerDepletionBehaviour : MonoBehaviour
{
	public string statusEffect = null!;

	public void Awake()
	{
		if (string.IsNullOrEmpty(statusEffect))
		{
			return;
		}

		if (Player.m_localPlayer && PassivePowers.DepletionEnabled())
		{
			Player.m_localPlayer.m_seman.AddStatusEffect(statusEffect.GetStableHashCode());
		}

		Destroy(gameObject);
	}
}
