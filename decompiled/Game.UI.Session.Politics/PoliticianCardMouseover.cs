using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim;
using Game.UI.Mouseovers;
using SomaSim.Util;

namespace Game.UI.Session.Politics;

internal class PoliticianCardMouseover : BaseCustomTextMouseover
{
	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTL;

	protected override string ProduceText()
	{
		PoliticianCardCtx componentInObjectOrParents = context.GetComponentInObjectOrParents<PoliticianCardCtx>();
		PoliticianData politicianData = Game.ctx.simman.politics.GetPoliticianData(componentInObjectOrParents.candidate);
		string text;
		string text2;
		switch (componentInObjectOrParents.type)
		{
		case PoliticianCardCtx.CtxType.ArchetypeInfo:
		{
			PoliticsSettings.PoliticalArchetype politicalArchetype = Game.serv.globals.settings.politics.FindPoliticalArchetype(politicianData.archetypeId);
			text = Loc.Get(politicalArchetype.locname);
			text2 = Loc.Get(politicalArchetype.locdesc);
			break;
		}
		case PoliticianCardCtx.CtxType.EthnicityInfo:
			text = Loc.Get(componentInObjectOrParents.candidate.FindEntity().data.person.GetEthDef().loc.adjEthnicity);
			text2 = Loc.Get("ui.politician-info.ethnicity-effect");
			break;
		case PoliticianCardCtx.CtxType.PedigreeInfo:
		{
			bool flag = Game.ctx.simman.politics.IsPoliticianIncumbent(componentInObjectOrParents.candidate);
			text = (flag ? Loc.Get("ui.politician-info.is-incumbent", "won", politicianData.incumbencies) : Loc.Get("ui.politician-info.not-incumbent", "won", politicianData.incumbencies));
			text2 = GetIncumbencyKey(flag, politicianData.incumbencies);
			break;
		}
		case PoliticianCardCtx.CtxType.TraitsInfo:
			text = PersonInfoUtil.GenerateTraitsIcons(componentInObjectOrParents.candidate.FindEntity());
			text2 = PersonInfoUtil.GenerateTraitsList(componentInObjectOrParents.candidate.FindEntity(), showDesc: true, showDescLong: false);
			break;
		default:
			return "";
		}
		return Loc.Get("ui.politics.candidate.info-mo", "title", text, "desc", text2);
		static string GetIncumbencyKey(bool isIncumbent, int incumbencies)
		{
			if (isIncumbent)
			{
				return Loc.Get("ui.politician-info.incumbent.desc");
			}
			if (incumbencies >= 1)
			{
				return Loc.Get("ui.politician-info.won-before.desc");
			}
			return Loc.Get("ui.politician-info.never-won.desc");
		}
	}
}
