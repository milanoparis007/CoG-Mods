using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Core;
using Game.Services.Store;
using SomaSim.Util;
using UnityEngine;

namespace Game.Services;

public sealed class PortraitMakerService : AbstractService
{
	public struct Piece
	{
		public string asset;

		public PackID? requires;

		public Label npcEthLocked;

		public string hatmaskOverride;

		public Piece(string asset, PackID? pack = null, Label? eth = null, string hatmaskOverride = null)
		{
			this.asset = asset;
			requires = pack;
			npcEthLocked = eth ?? Label.NULL;
			this.hatmaskOverride = hatmaskOverride;
		}

		public bool IsUnlocked(List<PackID> installedPacks)
		{
			if (requires.HasValue)
			{
				return installedPacks.Contains(requires.Value);
			}
			return true;
		}

		public bool IsValidForEth(Label eth)
		{
			if (!npcEthLocked.IsNotSet)
			{
				if (npcEthLocked.IsSet)
				{
					return eth == npcEthLocked;
				}
				return false;
			}
			return true;
		}
	}

	public sealed class GenderKeyedDictionary : Dictionary<Gender, List<Piece>>
	{
		public GenderKeyedDictionary()
			: base((IEqualityComparer<Gender>)new GenderEqualityComparer())
		{
		}

		public string FindOrNull(Gender gender, int hash, string debug, List<PackID> installedPacks, Label eth)
		{
			List<Piece> list = this.FindOrNull(gender);
			if (list == null || list.Count == 0)
			{
				return null;
			}
			using ListPool<string>.PooledBlockList pooledBlockList = ListPool<string>.Allocate();
			foreach (Piece item in list)
			{
				bool num = item.IsUnlocked(installedPacks);
				bool flag = item.IsValidForEth(eth);
				if (num && flag)
				{
					pooledBlockList.Add(item.asset);
				}
			}
			int num2 = Math.Abs(hash);
			return pooledBlockList[num2 % pooledBlockList.Count];
		}

		public string GetHatmaskOverride(Gender gender, int hash, List<PackID> installedPacks, Label eth)
		{
			List<Piece> list = this.FindOrNull(gender);
			if (list == null || list.Count == 0)
			{
				return null;
			}
			using ListPool<Piece>.PooledBlockList pooledBlockList = ListPool<Piece>.Allocate();
			foreach (Piece item in list)
			{
				bool num = item.IsUnlocked(installedPacks);
				bool flag = item.IsValidForEth(eth);
				if (num && flag)
				{
					pooledBlockList.Add(item);
				}
			}
			int num2 = Math.Abs(hash);
			return pooledBlockList[num2 % pooledBlockList.Count].hatmaskOverride;
		}
	}

	public sealed class PortraitDictionary : Dictionary<Skin, GenderKeyedDictionary>
	{
		public PortraitDictionary()
			: base((IEqualityComparer<Skin>)new SkinEqualityComparer())
		{
		}

		public string FindOrNull(Skin skin, Gender gender, int hash, string debug, List<PackID> installedPacks, Label eth)
		{
			Gender gender2 = ((gender == Gender.U) ? Gender.M : gender);
			Skin key = ((skin == Skin.Unknown) ? Skin.Light : skin);
			return this.FindOrNull(key)?.FindOrNull(gender2, hash, debug, installedPacks, eth);
		}
	}

	public static readonly string RESOURCES_FOLDER = "UI Images/Portrait Pieces/";

	public static readonly float HAT_PROB_FEDS = 0f;

	public static readonly float HAT_PROB_COPS = 1f;

	public static readonly float HAT_PROB_ALL = 0.7f;

	public static readonly PortraitDictionary BUSTS_ALL = new PortraitDictionary
	{
		{
			Skin.Dark,
			new GenderKeyedDictionary
			{
				{
					Gender.F,
					new List<Piece>
					{
						new Piece("Char_F_Black_Bust1"),
						new Piece("Char_F_Black_Bust2"),
						new Piece("Char_F_Black_Bust3"),
						new Piece("Char_F_Black_Bust4"),
						new Piece("Char_F_Black_Bust5"),
						new Piece("Char_F_Black_Bust6", PackID.AtlanticCity),
						new Piece("Char_F_Black_Bust7", PackID.AtlanticCity),
						new Piece("Pre_Char_F_Black_Bust6", PackID.Preorder)
					}
				},
				{
					Gender.M,
					new List<Piece>
					{
						new Piece("Char_M_Black_Bust1"),
						new Piece("Char_M_Black_Bust2"),
						new Piece("Char_M_Black_Bust3"),
						new Piece("Char_M_Black_Bust4"),
						new Piece("Char_M_Black_Bust5"),
						new Piece("Char_M_Black_Bust6"),
						new Piece("Char_M_Black_Bust7"),
						new Piece("Char_M_Black_Bust8"),
						new Piece("Char_M_Black_Bust9", PackID.AtlanticCity),
						new Piece("Char_M_Black_Bust10", PackID.AtlanticCity),
						new Piece("Char_M_Black_Bust11", PackID.AtlanticCity),
						new Piece("Pre_Char_M_Black_Bust9", PackID.Preorder),
						new Piece("Pre_Char_M_Black_Bust10", PackID.Preorder),
						new Piece("Pre_Char_M_Black_Bust11", PackID.Preorder)
					}
				}
			}
		},
		{
			Skin.Medium,
			new GenderKeyedDictionary
			{
				{
					Gender.F,
					new List<Piece>
					{
						new Piece("Char_F_Tan_Bust1"),
						new Piece("Char_F_Tan_Bust2"),
						new Piece("Char_F_Tan_Bust3"),
						new Piece("Char_F_Tan_Bust4"),
						new Piece("Char_F_Tan_Bust5"),
						new Piece("Char_F_Tan_Bust6", PackID.AtlanticCity),
						new Piece("Char_F_Tan_Bust7", PackID.AtlanticCity),
						new Piece("Pre_Char_F_Tan_Bust6", PackID.Preorder)
					}
				},
				{
					Gender.M,
					new List<Piece>
					{
						new Piece("Char_M_Tan_Bust1"),
						new Piece("Char_M_Tan_Bust2"),
						new Piece("Char_M_Tan_Bust3"),
						new Piece("Char_M_Tan_Bust4"),
						new Piece("Char_M_Tan_Bust5"),
						new Piece("Char_M_Tan_Bust6"),
						new Piece("Char_M_Tan_Bust7"),
						new Piece("Char_M_Tan_Bust8", PackID.AtlanticCity),
						new Piece("Char_M_Tan_Bust9", PackID.AtlanticCity),
						new Piece("Char_M_Tan_Bust10", PackID.AtlanticCity),
						new Piece("Pre_Char_M_Tan_Bust8", PackID.Preorder),
						new Piece("Pre_Char_M_Tan_Bust9", PackID.Preorder),
						new Piece("Pre_Char_M_Tan_Bust10", PackID.Preorder)
					}
				}
			}
		},
		{
			Skin.Light,
			new GenderKeyedDictionary
			{
				{
					Gender.F,
					new List<Piece>
					{
						new Piece("Char_F_White_Bust1"),
						new Piece("Char_F_White_Bust2"),
						new Piece("Char_F_White_Bust3"),
						new Piece("Char_F_White_Bust4"),
						new Piece("Char_F_White_Bust5"),
						new Piece("Char_F_White_Bust6"),
						new Piece("Char_F_White_Bust7"),
						new Piece("Char_F_White_Bust8", PackID.AtlanticCity),
						new Piece("Char_F_White_Bust9", PackID.AtlanticCity),
						new Piece("Pre_Char_F_White_Bust8", PackID.Preorder)
					}
				},
				{
					Gender.M,
					new List<Piece>
					{
						new Piece("Char_M_White_Bust1"),
						new Piece("Char_M_White_Bust2"),
						new Piece("Char_M_White_Bust3"),
						new Piece("Char_M_White_Bust4"),
						new Piece("Char_M_White_Bust5"),
						new Piece("Char_M_White_Bust6"),
						new Piece("Char_M_White_Bust7"),
						new Piece("Char_M_White_Bust8"),
						new Piece("Char_M_White_Bust9"),
						new Piece("Char_M_White_Bust10"),
						new Piece("Char_M_White_Bust11"),
						new Piece("Char_M_White_Bust12", PackID.AtlanticCity),
						new Piece("Char_M_White_Bust13", PackID.AtlanticCity),
						new Piece("Char_M_White_Bust14", PackID.AtlanticCity),
						new Piece("Pre_Char_M_White_Bust11", PackID.Preorder),
						new Piece("Pre_Char_M_White_Bust12", PackID.Preorder),
						new Piece("Pre_Char_M_White_Bust13", PackID.Preorder)
					}
				}
			}
		}
	};

	public static readonly PortraitDictionary BUSTS_COPS = new PortraitDictionary
	{
		{
			Skin.Dark,
			new GenderKeyedDictionary
			{
				{
					Gender.F,
					new List<Piece>
					{
						new Piece("Char_F_Black_Bust2")
					}
				},
				{
					Gender.M,
					new List<Piece>
					{
						new Piece("Char_M_Black_BustPolice1"),
						new Piece("Char_M_Black_BustPolice2"),
						new Piece("Char_M_Black_BustPolice3")
					}
				}
			}
		},
		{
			Skin.Medium,
			new GenderKeyedDictionary
			{
				{
					Gender.F,
					new List<Piece>
					{
						new Piece("Char_F_Tan_Bust1")
					}
				},
				{
					Gender.M,
					new List<Piece>
					{
						new Piece("Char_M_Tan_BustPolice1"),
						new Piece("Char_M_Tan_BustPolice2"),
						new Piece("Char_M_Tan_BustPolice3")
					}
				}
			}
		},
		{
			Skin.Light,
			new GenderKeyedDictionary
			{
				{
					Gender.F,
					new List<Piece>
					{
						new Piece("Char_F_White_Bust4")
					}
				},
				{
					Gender.M,
					new List<Piece>
					{
						new Piece("Char_M_White_BustPolice1"),
						new Piece("Char_M_White_BustPolice2"),
						new Piece("Char_M_White_BustPolice3")
					}
				}
			}
		}
	};

	public static readonly PortraitDictionary BUSTS_FEDS = new PortraitDictionary
	{
		{
			Skin.Dark,
			new GenderKeyedDictionary
			{
				{
					Gender.F,
					new List<Piece>
					{
						new Piece("Char_F_Black_Bust2")
					}
				},
				{
					Gender.M,
					new List<Piece>
					{
						new Piece("Char_M_Black_Bust8")
					}
				}
			}
		},
		{
			Skin.Medium,
			new GenderKeyedDictionary
			{
				{
					Gender.F,
					new List<Piece>
					{
						new Piece("Char_F_Tan_Bust1")
					}
				},
				{
					Gender.M,
					new List<Piece>
					{
						new Piece("Char_M_Tan_Bust2")
					}
				}
			}
		},
		{
			Skin.Light,
			new GenderKeyedDictionary
			{
				{
					Gender.F,
					new List<Piece>
					{
						new Piece("Char_F_White_Bust2")
					}
				},
				{
					Gender.M,
					new List<Piece>
					{
						new Piece("Char_M_White_Bust8"),
						new Piece("Char_M_White_Bust9")
					}
				}
			}
		}
	};

	public static readonly PortraitDictionary HEADS_ALL = new PortraitDictionary
	{
		{
			Skin.Dark,
			new GenderKeyedDictionary
			{
				{
					Gender.F,
					new List<Piece>
					{
						new Piece("Char_F_Black_Head1"),
						new Piece("Char_F_Black_Head2")
					}
				},
				{
					Gender.M,
					new List<Piece>
					{
						new Piece("Char_M_Black_Head1"),
						new Piece("Char_M_Black_Head2"),
						new Piece("Char_M_Black_Head3"),
						new Piece("Char_M_Black_Head4"),
						new Piece("Char_M_Black_Head5", PackID.AtlanticCity)
					}
				}
			}
		},
		{
			Skin.Medium,
			new GenderKeyedDictionary
			{
				{
					Gender.F,
					new List<Piece>
					{
						new Piece("Char_F_Tan_Head1"),
						new Piece("Char_F_Tan_Head2"),
						new Piece("Pre_Char_F_Tan_Head3", PackID.Preorder)
					}
				},
				{
					Gender.M,
					new List<Piece>
					{
						new Piece("Char_M_Tan_Head1"),
						new Piece("Char_M_Tan_Head2"),
						new Piece("Char_M_Tan_Head3"),
						new Piece("Pre_Char_M_Tan_Head4", PackID.Preorder)
					}
				}
			}
		},
		{
			Skin.Light,
			new GenderKeyedDictionary
			{
				{
					Gender.F,
					new List<Piece>
					{
						new Piece("Char_F_White_Head1"),
						new Piece("Char_F_White_Head2"),
						new Piece("Char_F_White_Head3"),
						new Piece("Char_F_White_Head4"),
						new Piece("Char_F_White_Head5"),
						new Piece("Char_F_White_Head6", PackID.AtlanticCity),
						new Piece("Char_F_White_Head7", PackID.AtlanticCity)
					}
				},
				{
					Gender.M,
					new List<Piece>
					{
						new Piece("Char_M_White_Head1"),
						new Piece("Char_M_White_Head2"),
						new Piece("Char_M_White_Head3"),
						new Piece("Char_M_White_Head4"),
						new Piece("Char_M_White_Head5"),
						new Piece("Char_M_White_Head6", PackID.AtlanticCity),
						new Piece("Char_M_White_Head7", PackID.AtlanticCity),
						new Piece("Pre_Char_M_White_Head6", PackID.Preorder)
					}
				}
			}
		}
	};

	public static readonly GenderKeyedDictionary HAT_MASKS = new GenderKeyedDictionary
	{
		{
			Gender.F,
			new List<Piece>
			{
				new Piece("HatMask_F")
			}
		},
		{
			Gender.M,
			new List<Piece>
			{
				new Piece("HatMask_M")
			}
		}
	};

	public static readonly GenderKeyedDictionary HATS_ALL = new GenderKeyedDictionary
	{
		{
			Gender.F,
			new List<Piece>
			{
				new Piece("Char_F_Hat1"),
				new Piece("Char_F_Hat2"),
				new Piece("Char_F_Hat3"),
				new Piece("Char_F_Hat4"),
				new Piece("Char_F_Hat5", PackID.AtlanticCity),
				new Piece("Char_F_Hat6", PackID.AtlanticCity),
				new Piece("Pre_Char_F_Hat5", PackID.Preorder),
				new Piece("Char_DE_Hat1", PackID.EthPackDE, new Label("de"), "HatMask_M"),
				new Piece("Char_DE_Hat2", PackID.EthPackDE, new Label("de"), "HatMask_M"),
				new Piece("Char_EN_Hat1", PackID.EthPackEN, new Label("en"), "HatMask_M"),
				new Piece("Char_EN_Hat2", PackID.EthPackEN, new Label("en"), "HatMask_M"),
				new Piece("Char_IR_Hat1", PackID.EthPackIR, new Label("ir"), "HatMask_M"),
				new Piece("Char_IR_Hat2", PackID.EthPackIR, new Label("ir"), "HatMask_M"),
				new Piece("Char_IT_Hat1", PackID.EthPackIT, new Label("it"), "HatMask_M"),
				new Piece("Char_IT_Hat2", PackID.EthPackIT, new Label("it"), "HatMask_M"),
				new Piece("Char_PL_Hat1", PackID.EthPackPL, new Label("pl"), "HatMask_M"),
				new Piece("Char_PL_Hat2", PackID.EthPackPL, new Label("pl"), "HatMask_M")
			}
		},
		{
			Gender.M,
			new List<Piece>
			{
				new Piece("Char_M_Hat1"),
				new Piece("Char_M_Hat2"),
				new Piece("Char_M_Hat3"),
				new Piece("Char_M_Hat4"),
				new Piece("Char_M_Hat5"),
				new Piece("Char_M_Hat6"),
				new Piece("Char_M_Hat7"),
				new Piece("Char_M_Hat8", PackID.AtlanticCity),
				new Piece("Char_M_Hat9", PackID.AtlanticCity),
				new Piece("Char_M_Hat10", PackID.AtlanticCity),
				new Piece("Pre_Char_M_Hat8", PackID.Preorder),
				new Piece("Pre_Char_M_Hat9", PackID.Preorder),
				new Piece("Char_DE_Hat1", PackID.EthPackDE, new Label("de"), "HatMask_M"),
				new Piece("Char_DE_Hat2", PackID.EthPackDE, new Label("de"), "HatMask_M"),
				new Piece("Char_EN_Hat1", PackID.EthPackEN, new Label("en"), "HatMask_M"),
				new Piece("Char_EN_Hat2", PackID.EthPackEN, new Label("en"), "HatMask_M"),
				new Piece("Char_IR_Hat1", PackID.EthPackIR, new Label("ir"), "HatMask_M"),
				new Piece("Char_IR_Hat2", PackID.EthPackIR, new Label("ir"), "HatMask_M"),
				new Piece("Char_IT_Hat1", PackID.EthPackIT, new Label("it"), "HatMask_M"),
				new Piece("Char_IT_Hat2", PackID.EthPackIT, new Label("it"), "HatMask_M"),
				new Piece("Char_PL_Hat1", PackID.EthPackPL, new Label("pl"), "HatMask_M"),
				new Piece("Char_PL_Hat2", PackID.EthPackPL, new Label("pl"), "HatMask_M")
			}
		}
	};

	public static readonly GenderKeyedDictionary HATS_COPS = new GenderKeyedDictionary
	{
		{
			Gender.F,
			new List<Piece>
			{
				new Piece("Char_M_HatPolice1"),
				new Piece("Char_M_HatPolice2"),
				new Piece("Char_M_HatPolice3")
			}
		},
		{
			Gender.M,
			new List<Piece>
			{
				new Piece("Char_M_HatPolice1"),
				new Piece("Char_M_HatPolice2"),
				new Piece("Char_M_HatPolice3")
			}
		}
	};

	public static readonly GenderKeyedDictionary HATS_FEDS = new GenderKeyedDictionary
	{
		{
			Gender.F,
			new List<Piece>
			{
				new Piece("Char_M_Hat1"),
				new Piece("Char_M_Hat2")
			}
		},
		{
			Gender.M,
			new List<Piece>
			{
				new Piece("Char_M_Hat1"),
				new Piece("Char_M_Hat2"),
				new Piece("Char_M_Hat3")
			}
		}
	};

	public PortraitInfo GetPortraitPieces(int hash, int hathash, Gender g, Skin s, bool iscop, bool isfed, Label eth)
	{
		List<PackID> installedPacks = Game.serv.store.FindAllInstalledPacks().ToList();
		GenderKeyedDictionary genderKeyedDictionary = (isfed ? HATS_FEDS : (iscop ? HATS_COPS : HATS_ALL));
		PortraitDictionary portraitDictionary = (isfed ? BUSTS_FEDS : (iscop ? BUSTS_COPS : BUSTS_ALL));
		PortraitDictionary hEADS_ALL = HEADS_ALL;
		GenderKeyedDictionary hAT_MASKS = HAT_MASKS;
		bool flag = false;
		if (hash != 0)
		{
			Xorshift rng = new Xorshift((uint)hash);
			float probability = (isfed ? HAT_PROB_FEDS : (iscop ? HAT_PROB_COPS : HAT_PROB_ALL));
			if (rng.CheckProbability(probability))
			{
				flag = true;
			}
		}
		return new PortraitInfo
		{
			hash = hash,
			hathash = hathash,
			hatmask = (genderKeyedDictionary.GetHatmaskOverride(g, hash, installedPacks, eth) ?? hAT_MASKS.FindOrNull(g, hash, "HAT_MASKS", installedPacks, eth)),
			hat = (flag ? genderKeyedDictionary.FindOrNull(g, hathash, "HATS", installedPacks, eth) : null),
			head = hEADS_ALL.FindOrNull(s, g, hash, "HEADS", installedPacks, eth),
			bust = portraitDictionary.FindOrNull(s, g, hash, "BUSTS", installedPacks, eth),
			iscop = iscop,
			isfed = isfed
		};
	}

	public (List<Piece> faces, List<Piece> busts, List<Piece> hats) GetAllPortraitPiecesForCustom(Gender g, Skin s)
	{
		List<PackID> packs = Game.serv.store.FindAllInstalledPacks().ToList();
		List<Piece> item = new List<Piece>(BUSTS_ALL[s][g]).Where((Piece x) => x.IsUnlocked(packs)).ToList();
		List<Piece> item2 = new List<Piece>(HEADS_ALL[s][g]).Where((Piece x) => x.IsUnlocked(packs)).ToList();
		List<Piece> item3 = new List<Piece>(HATS_ALL[g]).Where((Piece x) => x.IsUnlocked(packs)).ToList();
		return (faces: item2, busts: item, hats: item3);
	}

	public override void OnStartLoading()
	{
	}

	[Conditional("UNITY_EDITOR")]
	private void VerifyPortraitPieces()
	{
	}

	[Conditional("UNITY_EDITOR")]
	private void Verify(PortraitDictionary dict)
	{
		foreach (KeyValuePair<Skin, GenderKeyedDictionary> item in dict)
		{
			_ = item;
		}
	}

	[Conditional("UNITY_EDITOR")]
	private void Verify(GenderKeyedDictionary dict)
	{
		foreach (KeyValuePair<Gender, List<Piece>> item in dict)
		{
			foreach (Piece item2 in item.Value)
			{
				_ = item2;
			}
		}
	}

	[Conditional("UNITY_EDITOR")]
	private void Verify(Piece sprite)
	{
		Texture2D texture2D = FindTexture(sprite.asset);
		if (texture2D == null)
		{
			Logger.Warning($"Portrait loading for sprite {sprite} => {texture2D != null}");
		}
	}

	private static Texture2D FindTexture(string name)
	{
		if (name == null)
		{
			return null;
		}
		return Resources.Load(RESOURCES_FOLDER + name) as Texture2D;
	}

	private void ResetTexture(Texture2D targetTx)
	{
		Color32 color = Color.clear;
		Color32[] pixels = targetTx.GetPixels32();
		int i = 0;
		for (int num = pixels.Length; i < num; i++)
		{
			pixels[i] = color;
		}
		targetTx.SetPixels32(pixels);
		targetTx.Apply();
	}

	public Sprite GenerateBlankPortrait(PortraitInfo info)
	{
		Texture2D texture2D = FindTexture(info.head);
		Texture2D texture2D2 = new Texture2D(texture2D.width, texture2D.height, texture2D.format, mipChain: true)
		{
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Trilinear,
			mipMapBias = -1.5f
		};
		ResetTexture(texture2D2);
		return Sprite.Create(texture2D2, new Rect(0f, 0f, texture2D2.width, texture2D2.height), new Vector2(0f, 0f));
	}

	public void ResetPortrait(Sprite sprite)
	{
		ResetTexture(sprite.texture);
	}

	public void PopulateCompositePortrait(PortraitInfo info, Sprite sprite)
	{
		Texture2D texture2D = FindTexture(info.hat);
		Texture2D overlayTx = FindTexture(info.head);
		Texture2D overlayTx2 = FindTexture(info.bust);
		Texture2D texture = sprite.texture;
		CopyTexture(overlayTx2, texture);
		CopyTexture(overlayTx, texture);
		if (texture2D != null)
		{
			ClipBehindMask(FindTexture(info.hatmask), texture);
			CopyTexture(texture2D, texture);
		}
		static void ClipBehindMask(Texture2D maskTx, Texture2D targetTx)
		{
			Color32[] pixels = maskTx.GetPixels32();
			Color32[] pixels2 = targetTx.GetPixels32();
			Color32 color = Color.clear;
			int i = 0;
			for (int num = pixels2.Length; i < num; i++)
			{
				if (pixels[i].a > 0)
				{
					pixels2[i] = color;
				}
			}
			targetTx.SetPixels32(pixels2);
			targetTx.Apply();
		}
		static void CopyTexture(Texture2D texture2D2, Texture2D targetTx)
		{
			Color32[] pixels = texture2D2.GetPixels32();
			Color32[] pixels2 = targetTx.GetPixels32();
			int i = 0;
			for (int num = pixels2.Length; i < num; i++)
			{
				Color32 color = pixels2[i];
				Color32 color2 = pixels[i];
				float num2 = (float)(int)color2.a / 255f;
				float num3 = 1f - num2;
				Color32 color3 = new Color32((byte)((float)(int)color.r * num3 + (float)(int)color2.r * num2), (byte)((float)(int)color.g * num3 + (float)(int)color2.g * num2), (byte)((float)(int)color.b * num3 + (float)(int)color2.b * num2), (color.a > color2.a) ? color.a : color2.a);
				pixels2[i] = color3;
			}
			targetTx.SetPixels32(pixels2);
			targetTx.Apply();
		}
	}

	public void DestroyPortrait(Sprite sprite)
	{
		UnityEngine.Object.Destroy(sprite.texture);
		UnityEngine.Object.Destroy(sprite);
	}
}
