using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Sim.Modules;
using SomaSim.SION;

namespace Game.Session.Sim;

public sealed class ResourceManager : AbstractSessionManager, ISaveLoadProvider
{
	private ResourceManagerPersistedData _rdata;

	public LabelDictionary<Resource> resourcesCache = new LabelDictionary<Resource>();

	public ModuleCommon replaceCommon;

	public readonly Label ETHNIC_BOOZE = new Label("ethnic-alcohol");

	public readonly Label ETHNIC_BOOZE_PRODUCTION = new Label("production-ethnic-alcohol");

	public override void OnInitializeStarted()
	{
		base.OnInitializeStarted();
		_rdata = new ResourceManagerPersistedData();
		InitializeResourceCache();
	}

	public void InitializeResourceCache()
	{
		Label ethnicity = Game.ctx.session.scenario.newgamepars.playerdetails.player.ethnicity;
		foreach (KeyValuePair<Label, Resource> definition in Game.serv.globals.settings.resources.definitions)
		{
			if (ETHNIC_BOOZE == definition.Key)
			{
				bool flag = false;
				foreach (KeyValuePair<Label, EthnicAlcoholReplacements> ethPackAlcohol in Game.serv.globals.settings.resources.ethPackAlcohols)
				{
					if (ethPackAlcohol.Value.ethId == ethnicity)
					{
						if (ethPackAlcohol.Value.resource != null)
						{
							ethPackAlcohol.Value.resource.resid = ETHNIC_BOOZE;
							ethPackAlcohol.Value.resource.unitdef = Game.serv.globals.settings.resources.FindUnit(ethPackAlcohol.Value.resource.unitid);
							resourcesCache.Add(ETHNIC_BOOZE, ethPackAlcohol.Value.resource);
							flag = true;
						}
						replaceCommon = ethPackAlcohol.Value.common;
						break;
					}
				}
				if (!flag)
				{
					resourcesCache.Add(definition.Key, definition.Value);
				}
			}
			else
			{
				resourcesCache.Add(definition.Key, definition.Value);
			}
		}
	}

	public bool IsForcedIllegal(Label resId)
	{
		return _rdata.resourcesForcedIllegal.Contains(resId);
	}

	public bool IsForcedLegal(Label resId)
	{
		return _rdata.resourcesForcedLegal.Contains(resId);
	}

	public void ForceResourceIllegal(Label resId)
	{
		if (!_rdata.resourcesForcedIllegal.Contains(resId))
		{
			if (_rdata.resourcesForcedLegal.Contains(resId))
			{
				_rdata.resourcesForcedLegal.Remove(resId);
			}
			else
			{
				_rdata.resourcesForcedIllegal.Add(resId);
			}
		}
	}

	public void ForceResourceLegal(Label resId)
	{
		if (!_rdata.resourcesForcedLegal.Contains(resId))
		{
			if (_rdata.resourcesForcedIllegal.Contains(resId))
			{
				_rdata.resourcesForcedIllegal.Remove(resId);
			}
			else
			{
				_rdata.resourcesForcedLegal.Add(resId);
			}
		}
	}

	public void Save(Serializer s, ConcurrentSaveTable results)
	{
		results.Set("data", s.Serialize(_rdata));
	}

	public IEnumerator Load(Hashtable data)
	{
		SaveLoadUtils.DeserializeSingleKey(data, "data", delegate(ResourceManagerPersistedData result)
		{
			_rdata = result;
		});
		yield break;
	}
}
