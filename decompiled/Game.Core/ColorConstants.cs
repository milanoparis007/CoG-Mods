using System.Collections.Generic;
using SomaSim.Util;
using UnityEngine;

namespace Game.Core;

public static class ColorConstants
{
	public static Color NAV_COLOR = ColorUtil.HexToColor("#E39D3F");

	public static Color ARROW_BUY = ColorUtil.HexToColor("#B1674A");

	public static Color ARROW_SELL = ColorUtil.HexToColor("#728B4A");

	public static Color ARROW_SAFE = ColorUtil.HexToColor("#d0d0d0");

	public static Color ARROW_REL = ColorUtil.HexToColor("#c48039");

	public static Color ARROW_REL_HL = ColorUtil.HexToColor("#e6b559");

	public static string TEXT_HEX_GREEN = "#728b4a";

	public static string TEXT_HEX_RED = "#d35d39";

	public static string TEXT_HEX_WHITE = "#ffffff";

	public static string TEXT_HEX_DISABLED = "#878883";

	public static string TEXT_HEX_CONVO_DIMMED = "#b0b0b0";

	public static string TEXT_HEX_POSTCARD = "#1E211E";

	public static Color IMG_DISABLED = ColorUtil.HexToColor(TEXT_HEX_DISABLED);

	public static string TEXT_HEX_CONSTRUCTION = "#fbb833";

	public static string TEXT_HEX_RESPECT = "#728b4a";

	public static string TEXT_HEX_TRIBUTE = "#ae491f";

	public static string TEXT_HEX_XP = "#FBB833";

	public static Color OVERLAY_HEAT = ColorUtil.HexToColor("#d12c28");

	public static Color OVERLAY_RESPECT = ColorUtil.HexToColor("#728b4a");

	public static Color OVERLAY_RES = ColorUtil.HexToColor("859B51");

	public static Color OVERLAY_IND = ColorUtil.HexToColor("D0A42C");

	public static Color OVERLAY_COM = ColorUtil.HexToColor("649FBC");

	public static Color OVERLAY_ETH = ColorUtil.HexToColor("FF701B");

	public static Color OVERLAY_CARS = ColorUtil.HexToColor("A0A0E0");

	public static Color OVERLAY_DEFAULT = new Color32(147, 140, 34, 0);

	public static Color OVERLAY_ETH_DEFAULT = new Color32(147, 140, 130, 0);

	public static List<Color> PrecinctColors = new List<Color>
	{
		ColorUtil.HexToColor("#495799"),
		ColorUtil.HexToColor("#694799"),
		ColorUtil.HexToColor("#6298b4"),
		ColorUtil.HexToColor("#637c69"),
		ColorUtil.HexToColor("#6c62b4")
	};

	public static List<(Color primary, Color dark, Color alpha)> PoliticianColors = new List<(Color, Color, Color)>
	{
		(ColorUtil.HexToColor("#3d6997"), ColorUtil.HexToColor("#2d4e70"), ColorUtil.HexToColor("#2d4e70").SetAlpha(100)),
		(ColorUtil.HexToColor("#b5483c"), ColorUtil.HexToColor("#86352c"), ColorUtil.HexToColor("#86352c").SetAlpha(100)),
		(ColorUtil.HexToColor("#b29546"), ColorUtil.HexToColor("#836d33"), ColorUtil.HexToColor("#836d33").SetAlpha(100))
	};

	public static Color WON_ELECTION_HEADER_COLOR = ColorUtil.HexToColor("#4f437e");

	public static Color LOST_ELECTION_COLOR = ColorUtil.HexToColor("#833934");

	public static Color WON_ELECTION_COLOR = ColorUtil.HexToColor("#4F437E");

	public static Color POLICE_STATION = ColorUtil.HexToColor("#e6b559");

	public static Color POLICE_BLUE = ColorUtil.HexToColor("#273b9a");
}
