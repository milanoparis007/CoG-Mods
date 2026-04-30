using System.Diagnostics;
using Game.Core;

namespace Game.Services;

[DebuggerDisplay("{DebugString}")]
public sealed class EthnicityDef
{
	public Label id;

	public bool immigrant;

	public bool hidden;

	public EthnicityLockeys loc;

	private string DebugString => id.String;

	public override string ToString()
	{
		return id.String;
	}
}
