using Game.Core;
using UnityEngine;

namespace Game.UI.Session.Politics;

internal class PoliticianCardCtx : MonoBehaviour
{
	public enum CtxType
	{
		ArchetypeInfo,
		TraitsInfo,
		PedigreeInfo,
		EthnicityInfo
	}

	public CtxType type;

	public EntityID candidate;

	public void Set(CtxType type, EntityID candidate)
	{
		this.type = type;
		this.candidate = candidate;
	}
}
