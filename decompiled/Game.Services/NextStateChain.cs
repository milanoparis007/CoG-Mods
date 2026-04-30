using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;
using Game.UI.Session.Convo;

namespace Game.Services;

public class NextStateChain : NextStateDef
{
	public List<NextStateDef> defs;

	public Label @default;

	public override Label Evaluate(VisitState visit, ConvoButtonState bstate)
	{
		if (defs != null)
		{
			foreach (NextStateDef def in defs)
			{
				Label result = def.Evaluate(visit, bstate);
				if (result.IsSet)
				{
					return result;
				}
			}
		}
		return @default;
	}

	public override void Verify(ConvoButtonDef button)
	{
		if (defs != null)
		{
			_ = defs.Count > 0;
		}
		else
			_ = 0;
		_ = @default.IsSet;
	}
}
