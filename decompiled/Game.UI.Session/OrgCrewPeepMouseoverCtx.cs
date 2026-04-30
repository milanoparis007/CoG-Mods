using Game.Core;
using UnityEngine;

namespace Game.UI.Session;

public class OrgCrewPeepMouseoverCtx : MonoBehaviour
{
	public EntityID crew;

	public void Set(EntityID crew)
	{
		this.crew = crew;
	}
}
