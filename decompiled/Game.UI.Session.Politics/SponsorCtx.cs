using Game.Core;
using UnityEngine;

namespace Game.UI.Session.Politics;

public class SponsorCtx : MonoBehaviour
{
	public EntityID candidate;

	public bool isNomination;

	internal void Set(EntityID candidate, bool isNomination)
	{
		this.candidate = candidate;
		this.isNomination = isNomination;
	}
}
