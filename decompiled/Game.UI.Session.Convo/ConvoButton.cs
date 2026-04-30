using Game.Services;
using Game.Session.Data;

namespace Game.UI.Session.Convo;

public class ConvoButton
{
	public ConvoButtonState state;

	public int index;

	public ConvoButton(int index, ConvoButtonDef def, ConvoData data)
	{
		state = new ConvoButtonState(def, data);
		this.index = index;
	}

	public T GetData<T>() where T : ConvoData
	{
		return state.data as T;
	}

	public bool IsVisible(VisitState visit)
	{
		return state.def.visreqs?.AllPass(visit, state) ?? true;
	}

	public bool IsEnabled(VisitState visit)
	{
		if (state.data != null && !state.data.IsConvoStepEnabled(visit, state))
		{
			return false;
		}
		if (state.def.reqs != null && !state.def.reqs.AllPass(visit, state))
		{
			return false;
		}
		return true;
	}

	public string ProduceForcedReaction(ConversationModel model)
	{
		ConvoButtonDef.ConvoSimpleText text = state.def.text;
		if (text.npc != null)
		{
			string npc = text.npc;
			object[] replacements = state.data?.MakeReplacements(model.visit, 0);
			return Loc.Get(npc, replacements);
		}
		return text.dynnpc?.GetFirstBlurb(model, this);
	}

	public string ProduceButtonText(ConversationModel model, bool enabled)
	{
		string[] array = state.data?.MakeReplacements(model.visit, index);
		ConvoButtonDef.ConvoSimpleText text = state.def.text;
		string text2 = (enabled ? text.key : (text.dis ?? text.key));
		if (text2 != null)
		{
			object[] replacements = array;
			return Loc.Get(text2, replacements);
		}
		string firstBlurb = (enabled ? text.dynkey : (text.dyndis ?? text.dynkey)).GetFirstBlurb(model, this, array);
		if (firstBlurb != null)
		{
			return firstBlurb;
		}
		return "";
	}

	public string ProduceButtonIcon(ConversationModel model)
	{
		string text = state.data?.MakeIconOrNull();
		if (text == null)
		{
			text = state.def?.text.dynicon?.GetFirstBlurb(model, this);
		}
		if (text == null)
		{
			text = Loc.GetOrDefault(state.def.text.icon);
		}
		return text;
	}

	public string ProduceButtonMouseover(ConversationButtonContext ctx)
	{
		ConvoButtonDef.ConvoSimpleText text = state.def.text;
		ConversationModel model = ctx.dialog.Model;
		string text2 = "";
		string[] array = state.data?.MakeReplacements(model.visit, index);
		string text3 = ((text.addmo && text.key != null) ? (text.key + ".mo") : text.mo);
		if (text3 != null && Game.serv.loc.HasKey(text3))
		{
			object[] replacements = array;
			text2 = Loc.Get(text3, replacements);
		}
		if (text.dynmo != null)
		{
			text2 = text.dynmo.GetFirstBlurb(model, this, array);
		}
		if (!IsEnabled(model.visit))
		{
			if (state.def.reqs != null)
			{
				string text4 = state.def.reqs.Explain(model.visit, ctx.button.state);
				if (!string.IsNullOrEmpty(text4))
				{
					text2 = (text2 + "\n\n" + text4).Trim();
				}
			}
			if (state.data != null && !state.data.IsConvoStepEnabled(model.visit, ctx.button.state))
			{
				string text5 = state.data.ExplainConvoStepNotEnabled(model.visit, ctx.button.state);
				if (!string.IsNullOrEmpty(text5))
				{
					text2 = (text2 + "\n\n" + text5).Trim();
				}
			}
		}
		if (!string.IsNullOrWhiteSpace(text2))
		{
			return text2;
		}
		return null;
	}

	public virtual void PerformSelectionSideEffects(ConversationModel model)
	{
		state.def?.grants?.ApplyAll(new GrantContext(model.visit));
	}
}
