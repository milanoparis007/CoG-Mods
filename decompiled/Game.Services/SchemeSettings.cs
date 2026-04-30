using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Data;

namespace Game.Services;

public sealed class SchemeSettings : IValidatingSettings
{
	public ModValue concurrentSchemesAllowed;

	public List<SchemeDef> schemeDefs;

	public List<ChapterDef> chapterDefs;

	public void Validate()
	{
		foreach (SchemeDef schemeDef in schemeDefs)
		{
			chapterDefs.Select((ChapterDef x) => x.id).Contains(schemeDef.startup.firstChapter);
			foreach (ChapterDef chapterDef in chapterDefs)
			{
				foreach (Decision successChoice in chapterDef.successChoices)
				{
					if (successChoice.nextState.IsSet)
					{
						chapterDefs.Select((ChapterDef x) => x.id).Contains(successChoice.nextState);
					}
				}
				foreach (Decision failChoice in chapterDef.failChoices)
				{
					if (failChoice.nextState.IsSet)
					{
						chapterDefs.Select((ChapterDef x) => x.id).Contains(failChoice.nextState);
					}
				}
			}
		}
	}

	public SchemeDef FindSchemeById(Label id)
	{
		foreach (SchemeDef schemeDef in schemeDefs)
		{
			if (schemeDef.id == id)
			{
				return schemeDef;
			}
		}
		return null;
	}

	public ChapterDef FindChapterById(Label id)
	{
		foreach (ChapterDef chapterDef in chapterDefs)
		{
			if (chapterDef.id == id)
			{
				return chapterDef;
			}
		}
		return null;
	}

	public List<SchemeDef> GetAllSchemeDefs()
	{
		return schemeDefs;
	}
}
