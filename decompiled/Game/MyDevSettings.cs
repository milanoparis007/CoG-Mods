using System.Collections.Generic;

namespace Game;

public class MyDevSettings : DevSettings.IDevKeyProvider
{
	public Dictionary<string, string> DevKeys => new Dictionary<string, string>();
}
