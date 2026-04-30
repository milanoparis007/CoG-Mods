namespace Game.Session.Data;

public static class IModifierExtensions
{
	public static bool DoesNeed(this IModifier mod, ModQueryElement need)
	{
		return (mod.QueryMustProvide & need) == need;
	}
}
