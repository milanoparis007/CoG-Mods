using Game.Core;
using UnityEngine;

namespace Game.UI.Session;

public class OrgRoleAddMouseoverCtx : MonoBehaviour
{
	public Label roleId;

	public void Set(Label roleId)
	{
		this.roleId = roleId;
	}
}
