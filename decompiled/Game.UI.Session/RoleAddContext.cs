using Game.Session.Entities;
using UnityEngine;

namespace Game.UI.Session;

public class RoleAddContext : MonoBehaviour
{
	public Entity peep;

	public bool canPromote;

	public void Set(Entity peep, bool canPromote)
	{
		this.peep = peep;
		this.canPromote = canPromote;
	}
}
