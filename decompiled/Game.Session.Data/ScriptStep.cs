using Game.Core;

namespace Game.Session.Data;

public sealed class ScriptStep
{
	public CommandType type;

	public DeicticVariable argument;

	public string message;

	public Label label;
}
