using System.Collections.Generic;
using System.Linq;
using SomaSim.Util;

namespace Game.Services.Store;

public class StoreService : AbstractService
{
	public Dictionary<string, PackID[]> maplocks = new Dictionary<string, PackID[]>
	{
		{
			"minitest",
			new PackID[1]
		},
		{
			"cincinnati",
			new PackID[1] { PackID.Deluxe }
		},
		{
			"atlantic-city",
			new PackID[1] { PackID.AtlanticCity }
		},
		{
			"philadelphia",
			new PackID[1] { PackID.CriminalRecord }
		},
		{
			"new-york",
			new PackID[1] { PackID.ShadowGovernment }
		}
	};

	private Dictionary<PackID, bool> _installedPacks = new Dictionary<PackID, bool>(new PackIDEqualityComparer());

	public Listeners OnAfterDLCsReloaded;

	public StoreConnector handler { get; private set; }

	public override void OnInitialized()
	{
		base.OnInitialized();
		OnAfterDLCsReloaded = new Listeners();
		handler = MakeHandler();
		handler.Initialize();
		CacheInstalledPacks();
		Logger.LogAlways("Installed dlcs: " + string.Join(", ", FindAllInstalledPacks()));
	}

	public override void OnReleased()
	{
		base.OnReleased();
		handler.Release();
		handler = null;
		OnAfterDLCsReloaded = null;
	}

	private void CacheInstalledPacks()
	{
		_installedPacks.Clear();
		PackID[] valuesAsArray = EnumUtil<PackID>.GetValuesAsArray();
		foreach (PackID packID in valuesAsArray)
		{
			_installedPacks[packID] = handler.IsPackInstalled(packID);
		}
	}

	public void OnDLCsReloaded()
	{
		CacheInstalledPacks();
		OnAfterDLCsReloaded?.Invoke();
	}

	private StoreConnector MakeHandler()
	{
		return new StoreSteam();
	}

	public IEnumerable<PackID> FindAllInstalledPacks()
	{
		return from e in _installedPacks
			where e.Value
			select e.Key;
	}

	public bool IsPackInstalled(PackID id)
	{
		return _installedPacks.FindOrDefault(id, defaultValue: false);
	}

	public bool IsPreorderInstalled()
	{
		return IsPackInstalled(PackID.Preorder);
	}

	public bool IsMapAvailable(string mapid)
	{
		PackID[] array = maplocks.FindOrNull(mapid);
		if (array == null || array.Length == 0)
		{
			return true;
		}
		PackID[] array2 = array;
		foreach (PackID id in array2)
		{
			if (handler.IsPackInstalled(id))
			{
				return true;
			}
		}
		return false;
	}
}
