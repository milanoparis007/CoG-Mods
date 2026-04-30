using Game.Core;

namespace Game.Session.Entities;

public static class TagConstants
{
	public static readonly Label TAG_BUILDING_ALL = new Label("building");

	public static readonly Label TAG_RESIDENTIAL = new Label("res");

	public static readonly Label TAG_COMMERCIAL = new Label("com");

	public static readonly Label TAG_INDUSTRIAL = new Label("ind");

	public static readonly Label TAG_EMPTY_LOT = new Label("empty-lot");

	public static readonly Label TAG_RAIL_TERMINAL = new Label("rail-terminal");

	public static readonly Label TAG_SAFEHOUSE_BACKROOMS = new Label("tag-biz-back");

	public static readonly Label TAG_SAFEHOUSE_FRONTROOMS = new Label("tag-biz-front");

	public static readonly Label TAG_VERT_BOOZE = new Label("tag-vert-booze");

	public static readonly Label TAG_VERT_CARS = new Label("tag-vert-cars");

	public static readonly Label TAG_VERT_GAMBLING = new Label("tag-vert-gambling");

	public static readonly Label ALL_TAG_VERTS_PREFIX = new Label("tag-vert-");

	public static readonly Label[] ALL_TAG_VERTS = new Label[3] { TAG_VERT_BOOZE, TAG_VERT_CARS, TAG_VERT_GAMBLING };
}
