using Game.Core;
using SomaSim.Util;
using UnityEngine;

namespace Game.Services;

public struct PlayerColor
{
	public string hexcolor;

	public Color GetPlayerColor()
	{
		return ColorUtil.HexToColor(hexcolor);
	}

	public Color GetNavColor()
	{
		return ColorConstants.NAV_COLOR;
	}
}
