using System.Collections.Generic;

namespace Game.Services;

public sealed class EncyclopediaSettings
{
	public sealed class Category
	{
		public string locname;

		public List<string> entries = new List<string>();
	}

	public List<Category> categories = new List<Category>();
}
