using Game.Core;

namespace Game.Session.Entities;

public class Entity
{
	public EntityConfig config;

	public EntityComponents components;

	public EntityData data;

	public bool IsInitialized => data != null;

	public bool IsDestroyed => config == null;

	public bool IsEnabled
	{
		get
		{
			if (data != null)
			{
				return data.ident.enabled;
			}
			return false;
		}
	}

	public EntityID Id
	{
		get
		{
			if (data == null)
			{
				return 0uL;
			}
			return data.ident.id;
		}
	}

	public string Name
	{
		get
		{
			if (components == null)
			{
				return "?";
			}
			return components.ident.debugname;
		}
	}

	public void SetEnabled(bool value)
	{
		data.ident.enabled = value;
		components.SendEvent((!value) ? EntityEventType.EntityDisabled : EntityEventType.EntityEnabled);
	}

	public override string ToString()
	{
		return "[Entity " + Name + "]";
	}

	public static int Comparison(Entity x, Entity y)
	{
		return (int)(x.Id.id - y.Id.id);
	}
}
