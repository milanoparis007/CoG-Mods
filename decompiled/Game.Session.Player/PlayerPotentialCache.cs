using Game.Core;
using Game.Session.Board;

namespace Game.Session.Player;

public sealed class PlayerPotentialCache : PlayerCache<NodeID>
{
	internal void OnTerritoryChanged()
	{
		RefreshCache();
	}

	internal void OnTakeover()
	{
		RefreshCache();
	}

	internal void OnLoad()
	{
		RefreshCache();
	}

	protected override void RefreshCache()
	{
		_elements.Clear();
		foreach (NodeID item in _manager.territory.GetAllOwnedNodesUnsafe())
		{
			if (item.FindNode().potential.IsValid)
			{
				_elements.Add(item);
			}
		}
	}
}
