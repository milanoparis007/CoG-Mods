using System;
using System.Diagnostics;
using Game.Core;
using Game.Services;

namespace Game.Session.Player.KB;

[DebuggerDisplay("{DebugString}")]
public struct KBResult : IEquatable<KBResult>
{
	public static KBResult NULL;

	public Label queryId;

	public bool valid;

	public bool passed;

	public bool showpip;

	public bool IsValid => queryId.IsSet;

	public bool IsNotValid => queryId.IsNotSet;

	public string DebugString => $"[KBQ {queryId} valid:{valid} passed:{passed} showpip:{showpip}]";

	public KBResult(Label queryId, bool valid, bool passed, bool showpip)
	{
		this.queryId = queryId;
		this.valid = valid;
		this.passed = passed;
		this.showpip = showpip;
	}

	public UIQuery FindQuery()
	{
		return Game.serv.globals.ui.FindQueryOrNull(queryId);
	}

	public static bool Equals(KBResult a, KBResult b)
	{
		if (Label.Equals(a.queryId, b.queryId) && a.valid == b.valid && a.passed == b.passed)
		{
			return a.showpip == b.showpip;
		}
		return false;
	}

	public bool Equals(KBResult other)
	{
		return Equals(this, other);
	}

	public override bool Equals(object obj)
	{
		if (obj is KBResult b)
		{
			return Equals(this, b);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return queryId.Index;
	}

	public override string ToString()
	{
		return DebugString;
	}
}
