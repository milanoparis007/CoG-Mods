namespace Game.Core;

public class ZoneTypeHelper
{
	public static ZoneTypeFlags ToFlags(ZoneType zone)
	{
		ZoneTypeFlags result = ZoneTypeFlags.None;
		switch (zone)
		{
		case ZoneType.Unknown:
			result = ZoneTypeFlags.None;
			break;
		case ZoneType.Res:
			result = ZoneTypeFlags.Res;
			break;
		case ZoneType.Com:
			result = ZoneTypeFlags.Com;
			break;
		case ZoneType.Ind:
			result = ZoneTypeFlags.Ind;
			break;
		}
		return result;
	}
}
