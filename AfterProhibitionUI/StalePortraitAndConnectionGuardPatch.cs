using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AfterProhibitionUI
{
	internal static class StalePortraitAndConnectionGuardPatch
	{
		private static readonly HashSet<string> NullFinalizerLogs = new HashSet<string>(StringComparer.Ordinal);
		private static readonly HashSet<string> SuppressLogs = new HashSet<string>(StringComparer.Ordinal);
		private static readonly Dictionary<Type, FieldInfo> GoFieldsByType = new Dictionary<Type, FieldInfo>();
		private static FieldInfo _basePickGoField;

		public static int ApplyPatch(Harmony harmony)
		{
			int patched = 0;
			patched += PatchConnectionsTab(harmony);
			patched += PatchCrewPick(harmony);
			patched += PatchDialogPortraits(harmony);
			AfterProhibitionUIPlugin.Log?.LogInfo("StalePortraitGuards patched=" + patched + " owner=AfterProhibitionUI");
			return patched;
		}

		private static int PatchConnectionsTab(Harmony harmony)
		{
			Type type = typeof(Entity).Assembly.GetType("Game.UI.Session.ConnectionsTabSubview");
			if (type == null)
			{
				AfterProhibitionUIPlugin.Log?.LogInfo("ConnectionsTab guard unavailable reason=type-missing");
				return 0;
			}

			int patched = 0;
			patched += PatchMethod(harmony, type, "RefreshAllCards", prefix: nameof(RefreshAllCardsPrefix), finalizer: nameof(NullRefFinalizer));
			patched += PatchMethod(harmony, type, "RefreshSubview", finalizer: nameof(NullRefFinalizer));
			patched += PatchMethod(harmony, type, "InitializeCard", prefix: nameof(InitializeCardPrefix), postfix: nameof(InitializeCardPostfix), finalizer: nameof(InitializeCardFinalizer));
			AfterProhibitionUIPlugin.Log?.LogInfo("ConnectionsTab render guard patched=" + patched);
			return patched;
		}

		private static int PatchCrewPick(Harmony harmony)
		{
			Type type = typeof(Entity).Assembly.GetType("Game.UI.Session.Picks.CrewPick");
			if (type == null)
			{
				return 0;
			}

			int patched = 0;
			patched += PatchMethod(harmony, type, "Reset", postfix: nameof(CrewPickResetPostfix));
			patched += PatchMethod(harmony, type, "SetTarget", prefix: nameof(CrewPickSetTargetPrefix));
			patched += PatchMethod(harmony, type, "RefreshContents", prefix: nameof(CrewPickRefreshContentsPrefix));
			return patched;
		}

		private static int PatchDialogPortraits(Harmony harmony)
		{
			int patched = 0;
			patched += PatchMethod(harmony, "Game.UI.Session.PersonInfoDialog", "RefreshContents", prefix: nameof(PersonInfoRefreshPrefix));
			patched += PatchMethod(harmony, "Game.UI.Session.PersonInfoDialog", "OnBeforeHide", postfix: nameof(PersonInfoHidePostfix));
			patched += PatchMethod(harmony, "Game.UI.Session.Convo.ConversationDialog", "RefreshContents", prefix: nameof(ConversationRefreshPrefix));
			patched += PatchMethod(harmony, "Game.UI.Session.Convo.ConversationDialog", "OnBeforeShow", prefix: nameof(ConversationRefreshPrefix));
			patched += PatchMethod(harmony, "Game.UI.Session.Convo.ConversationDialog", "OnBeforeHide", postfix: nameof(ConversationHidePostfix));
			patched += PatchMethod(harmony, "Game.UI.CrewPeepInspectPopup", "RefreshDialog", prefix: nameof(CrewInspectRefreshPrefix));
			patched += PatchMethod(harmony, "Game.UI.CrewPeepInspectPopup", "ReleaseOnPop", postfix: nameof(CrewInspectHidePostfix));
			patched += PatchMethod(harmony, "Game.UI.Session.Popups.PortraitPopup", "RefreshContents", prefix: nameof(PortraitPopupRefreshPrefix));
			patched += PatchMethod(harmony, "Game.UI.Session.Popups.PortraitPopup", "OnDeactivated", postfix: nameof(PortraitPopupHidePostfix));
			return patched;
		}

		private static int PatchMethod(Harmony harmony, string typeName, string methodName, string prefix = null, string postfix = null, string finalizer = null)
		{
			Type type = AccessTools.TypeByName(typeName) ?? typeof(Entity).Assembly.GetType(typeName);
			return type == null ? 0 : PatchMethod(harmony, type, methodName, prefix, postfix, finalizer);
		}

		private static int PatchMethod(Harmony harmony, Type type, string methodName, string prefix = null, string postfix = null, string finalizer = null)
		{
			MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null)
			{
				return 0;
			}

			harmony.Patch(
				original: method,
				prefix: string.IsNullOrEmpty(prefix) ? null : new HarmonyMethod(typeof(StalePortraitAndConnectionGuardPatch), prefix),
				postfix: string.IsNullOrEmpty(postfix) ? null : new HarmonyMethod(typeof(StalePortraitAndConnectionGuardPatch), postfix),
				transpiler: null,
				finalizer: string.IsNullOrEmpty(finalizer) ? null : new HarmonyMethod(typeof(StalePortraitAndConnectionGuardPatch), finalizer),
				ilmanipulator: null);
			return 1;
		}

		private static Exception NullRefFinalizer(Exception __exception, MethodBase __originalMethod)
		{
			if (__exception is NullReferenceException)
			{
				string key = __originalMethod?.Name ?? "unknown";
				if (NullFinalizerLogs.Add(key))
				{
					AfterProhibitionUIPlugin.Log?.LogInfo("ConnectionsTab null suppressed method=" + key + " owner=AfterProhibitionUI");
				}
				return null;
			}
			return __exception;
		}

		private static bool RefreshAllCardsPrefix(object __instance)
		{
			try
			{
				return !TryRefreshAllConnectionCards(__instance);
			}
			catch (Exception ex)
			{
				AfterProhibitionUIPlugin.Log?.LogWarning("ConnectionsTab robust refresh failed owner=AfterProhibitionUI error=" + ex.GetType().Name + ": " + ex.Message);
				return true;
			}
		}

		private static bool CreateFamilyLinksPrefix(object __instance, ref IList __result)
		{
			IList cards = BuildConnectionCards(__instance);
			if (cards == null)
			{
				return true;
			}

			__result = cards;
			return false;
		}

		private static Exception CreateFamilyLinksFinalizer(Exception __exception, MethodBase __originalMethod, object __instance, ref IList __result)
		{
			if (__exception is NullReferenceException)
			{
				IList cards = BuildConnectionCards(__instance) ?? CreateEmptyCardList();
				__result = cards;
				if (NullFinalizerLogs.Add("CreateFamilyLinks"))
				{
					AfterProhibitionUIPlugin.Log?.LogInfo("ConnectionsTab null suppressed method=CreateFamilyLinks fallbackCards=" + (cards?.Count ?? 0) + " owner=AfterProhibitionUI");
				}
				return null;
			}
			return NullRefFinalizer(__exception, __originalMethod);
		}

		private static bool InitializeCardPrefix(GameObject card, object data)
		{
			if (IsRenderableConnectionCard(data))
			{
				return true;
			}

			ClearConnectionCard(card);
			LogSuppressOnce("connection-render-suppressed", "ConnectionsTab invalid-card-render-suppressed owner=AfterProhibitionUI");
			return false;
		}

		private static void InitializeCardPostfix(object __instance, GameObject card, object data)
		{
			EnsureConnectionCardPortrait(card, data);
		}

		private static Exception InitializeCardFinalizer(Exception __exception, MethodBase __originalMethod, object __instance, GameObject card, object data)
		{
			if (__exception is NullReferenceException && IsRenderableConnectionCard(data))
			{
				RenderConnectionCardFallback(__instance, card, data);
				if (NullFinalizerLogs.Add("InitializeCard"))
				{
					AfterProhibitionUIPlugin.Log?.LogInfo("ConnectionsTab null suppressed method=InitializeCard fallback=card-render owner=AfterProhibitionUI");
				}
				return null;
			}
			return NullRefFinalizer(__exception, __originalMethod);
		}

		private static void CrewPickSetTargetPrefix(object __instance)
		{
			ClearCrewPickVisuals(ResolveGameObjectFromBasePick(__instance));
		}

		private static void CrewPickRefreshContentsPrefix(object __instance)
		{
			ClearCrewPickVisuals(ResolveGameObjectFromBasePick(__instance));
		}

		private static void CrewPickResetPostfix(object __instance)
		{
			ClearCrewPickVisuals(ResolveGameObjectFromBasePick(__instance));
		}

		private static void PersonInfoRefreshPrefix(object __instance)
		{
			GameObject root = ResolvePopupRoot(__instance);
			ClearImage(root, "Owner/Portrait/Portrait");
		}

		private static void PersonInfoHidePostfix(object __instance)
		{
			GameObject root = ResolvePopupRoot(__instance);
			ClearImage(root, "Owner/Portrait/Portrait");
		}

		private static void ConversationRefreshPrefix(object __instance)
		{
			GameObject root = ResolvePopupRoot(__instance);
			ClearImage(root, "Owner/Portrait/Portrait");
		}

		private static void ConversationHidePostfix(object __instance)
		{
			GameObject root = ResolvePopupRoot(__instance);
			ClearImage(root, "Owner/Portrait/Portrait");
		}

		private static void CrewInspectRefreshPrefix(object __instance)
		{
			GameObject root = ResolvePopupRoot(__instance);
			ClearImage(root, "Character/Biography/Mugshot/Portrait/Portrait");
		}

		private static void CrewInspectHidePostfix(object __instance)
		{
			GameObject root = ResolvePopupRoot(__instance);
			ClearImage(root, "Character/Biography/Mugshot/Portrait/Portrait");
		}

		private static void PortraitPopupRefreshPrefix(object __instance)
		{
			GameObject root = ResolvePopupRoot(__instance);
			ClearImage(root, "Panel/Portrait/Portrait");
		}

		private static void PortraitPopupHidePostfix(object __instance)
		{
			GameObject root = ResolvePopupRoot(__instance);
			ClearImage(root, "Panel/Portrait/Portrait");
			ClearText(root, "Panel/Text");
		}

		private static bool ShouldRevealHumanCrewConnections(object subviewInstance)
		{
			try
			{
				Entity entity = ResolveSubviewEntity(subviewInstance);
				if (entity == null || entity.data?.agent == null)
				{
					return false;
				}

				if (entity.data.agent.pid.IsHumanPlayer)
				{
					return true;
				}

				PlayerInfo human = CrewInfoActionBridge.GetHumanPlayer();
				return human?.crew != null && human.crew.GetCrewForPeep(entity.Id).IsValid;
			}
			catch
			{
				return false;
			}
		}

		private static Entity ResolveSubviewEntity(object subviewInstance)
		{
			object model = Traverse.Create(subviewInstance).Field("Model").GetValue();
			if (model == null)
			{
				return null;
			}

			FieldInfo fieldInfo = AccessTools.Field(model.GetType(), "entity");
			if (fieldInfo != null && fieldInfo.GetValue(model) is Entity entity)
			{
				return entity;
			}

			PropertyInfo propertyInfo = AccessTools.Property(model.GetType(), "entity");
			return propertyInfo?.GetValue(model, null) as Entity;
		}

		private static bool IsRenderableConnectionCard(object cardData)
		{
			if (cardData == null)
			{
				return false;
			}

			Entity cardPeep = AccessTools.Field(cardData.GetType(), "cardPeep")?.GetValue(cardData) as Entity;
			return IsRenderablePerson(cardPeep);
		}

		private static bool TryRefreshAllConnectionCards(object subviewInstance)
		{
			if (subviewInstance == null)
			{
				return false;
			}

			GameObject scrollView = AccessTools.Field(subviewInstance.GetType(), "_connScrollView")?.GetValue(subviewInstance) as GameObject;
			GameObject cardTemplate = AccessTools.Field(subviewInstance.GetType(), "_cardTemplate")?.GetValue(subviewInstance) as GameObject;
			GameObject content = scrollView?.transform.Find("Viewport/Content")?.gameObject;
			IList cards = BuildConnectionCards(subviewInstance);
			if (content == null || cardTemplate == null || cards == null)
			{
				return false;
			}

			EnsureChildCount(content, cards.Count, cardTemplate);
			MethodInfo initializeCard = subviewInstance.GetType().GetMethod("InitializeCard", BindingFlags.Instance | BindingFlags.NonPublic);
			for (int i = 0; i < cards.Count; i++)
			{
				GameObject card = content.transform.GetChild(i).gameObject;
				object data = cards[i];
				try
				{
					initializeCard?.Invoke(subviewInstance, new[] { card, data });
				}
				catch (Exception)
				{
					RenderConnectionCardFallback(subviewInstance, card, data);
				}
			}

			for (int i = cards.Count; i < content.transform.childCount; i++)
			{
				content.transform.GetChild(i).gameObject.SetActive(false);
			}

			return true;
		}

		private static void EnsureChildCount(GameObject container, int count, GameObject template)
		{
			if (container == null || template == null)
			{
				return;
			}

			while (container.transform.childCount < count)
			{
				GameObject child = UnityEngine.Object.Instantiate(template, container.transform);
				child.SetActive(false);
			}
		}

		private static IList BuildConnectionCards(object subviewInstance)
		{
			Type cardDataType = typeof(Entity).Assembly.GetType("Game.UI.Session.CardContextData");
			if (cardDataType == null)
			{
				return null;
			}

			IList result = CreateCardList(cardDataType);
			Entity source = ResolveSubviewEntity(subviewInstance);
			if (!IsRenderablePerson(source))
			{
				return result;
			}

			List<Relationship> relationships = global::Game.Game.ctx?.simman?.rels?.GetListOrNull(source.Id)?.data;
			if (relationships == null || relationships.Count == 0)
			{
				return result;
			}

			bool revealHumanCrewConnections = ShouldRevealHumanCrewConnections(subviewInstance);
			PlayerSocial social = global::Game.Game.ctx?.players?.Human?.social;
			SimTime now = global::Game.Game.ctx.clock.Now;
			List<object> cards = new List<object>(relationships.Count);
			int skipped = 0;
			int revealed = 0;

			for (int i = 0; i < relationships.Count; i++)
			{
				Relationship rel = relationships[i];
				Entity peep = rel?.to.FindEntity();
				if (!IsRenderablePerson(peep) || peep.data.person == source.data.person)
				{
					skipped++;
					continue;
				}

				object card = Activator.CreateInstance(cardDataType);
				AccessTools.Field(cardDataType, "cardPeep")?.SetValue(card, peep);
				AccessTools.Field(cardDataType, "overview")?.SetValue(card, PersonInfoUtil.GenerateOverview(peep, details: false));
				AccessTools.Field(cardDataType, "rel")?.SetValue(card, rel);

				bool known = social?.GetRelationshipFromSourceToPlayer(peep.Id) != null;
				if (!known && revealHumanCrewConnections)
				{
					known = true;
					revealed++;
				}

				AccessTools.Field(cardDataType, "isKnownByHuman")?.SetValue(card, known);
				AccessTools.Field(cardDataType, "isAlive")?.SetValue(card, peep.data.person.IsAlive);
				AccessTools.Field(cardDataType, "isAdult")?.SetValue(card, IsAdultForConnectionCard(peep, now));
				cards.Add(card);
			}

			cards.Sort(CompareConnectionCards);
			for (int i = 0; i < cards.Count; i++)
			{
				result.Add(cards[i]);
			}

			if (skipped > 0 || revealed > 0)
			{
				LogSuppressOnce(
					"connections-rebuilt",
					"ConnectionsTab rebuilt-card-targets count=" + result.Count + " skipped=" + skipped + " revealed=" + revealed + " owner=AfterProhibitionUI");
			}

			return result;
		}

		private static IList CreateEmptyCardList()
		{
			Type cardDataType = typeof(Entity).Assembly.GetType("Game.UI.Session.CardContextData");
			return cardDataType == null ? null : CreateCardList(cardDataType);
		}

		private static IList CreateCardList(Type cardDataType)
		{
			Type listType = typeof(List<>).MakeGenericType(cardDataType);
			return Activator.CreateInstance(listType) as IList;
		}

		private static int CompareConnectionCards(object a, object b)
		{
			Entity peepA = GetCardPeep(a);
			Entity peepB = GetCardPeep(b);

			if (IsAnyPlayer(peepA) && !IsAnyPlayer(peepB))
			{
				return -1;
			}

			if (IsAnyPlayer(peepB) && !IsAnyPlayer(peepA))
			{
				return 1;
			}

			bool employedA = peepA?.data?.person?.IsEmployed ?? false;
			bool employedB = peepB?.data?.person?.IsEmployed ?? false;
			if (employedA && !employedB)
			{
				return -1;
			}

			if (employedB && !employedA)
			{
				return 1;
			}

			bool aliveA = peepA?.data?.person?.IsAlive ?? false;
			bool aliveB = peepB?.data?.person?.IsAlive ?? false;
			if (!aliveB && aliveA)
			{
				return -1;
			}

			if (!aliveA && aliveB)
			{
				return 1;
			}

			int typeA = (int)(GetCardRelationship(a)?.type ?? RelationshipType.None);
			int typeB = (int)(GetCardRelationship(b)?.type ?? RelationshipType.None);
			return typeA.CompareTo(typeB);
		}

		private static bool IsAnyPlayer(Entity entity)
		{
			try
			{
				return entity?.data?.agent != null && entity.data.agent.pid.IsAnyPlayer;
			}
			catch
			{
				return false;
			}
		}

		private static bool IsAdultForConnectionCard(Entity peep, SimTime now)
		{
			try
			{
				return peep?.components?.person != null && peep.components.person.IsOldEnoughToOwnBiz(now);
			}
			catch
			{
				return false;
			}
		}

		private static Entity GetCardPeep(object data)
		{
			return data == null ? null : AccessTools.Field(data.GetType(), "cardPeep")?.GetValue(data) as Entity;
		}

		private static Relationship GetCardRelationship(object data)
		{
			return data == null ? null : AccessTools.Field(data.GetType(), "rel")?.GetValue(data) as Relationship;
		}

		private static bool IsRenderablePerson(Entity entity)
		{
			PersonData person = entity?.data?.person;
			if (person == null || !entity.Id.IsValid)
			{
				return false;
			}

			string fullName = person.FullName;
			return !string.IsNullOrWhiteSpace(fullName)
				&& !string.Equals(fullName.Trim(), "Name", StringComparison.OrdinalIgnoreCase);
		}

		private static void ClearConnectionCard(GameObject card)
		{
			if (card == null)
			{
				return;
			}

			ClearText(card, "Name");
			ClearImage(card, "Portrait/Portrait");
			SetChildActive(card, "Portrait", false);
			Button button = card.GetComponent<Button>();
			if (button != null)
			{
				button.onClick.RemoveAllListeners();
			}
			card.SetActive(false);
		}

		private static void RenderConnectionCardFallback(object subviewInstance, GameObject card, object data)
		{
			if (card == null || !IsRenderableConnectionCard(data))
			{
				ClearConnectionCard(card);
				return;
			}

			bool show = ShouldShowConnectionCard(subviewInstance, data);
			card.SetActive(show);
			if (!show)
			{
				return;
			}

			string label = ResolveConnectionCardLabel(subviewInstance, data) ?? GetCardPeep(data)?.data?.person?.FullName ?? string.Empty;
			SetTextValue(card, "Name", label);
			EnsureConnectionCardPortrait(card, data);
		}

		private static bool ShouldShowConnectionCard(object subviewInstance, object data)
		{
			object filter = ResolveSubviewConnectionFilter(subviewInstance);
			MethodInfo shouldShow = data?.GetType().GetMethod("ShouldShow", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (filter != null && shouldShow != null)
			{
				try
				{
					object value = shouldShow.Invoke(data, new[] { filter });
					return value is bool flag && flag;
				}
				catch
				{
					return true;
				}
			}

			return GetBoolField(data, "isKnownByHuman");
		}

		private static object ResolveSubviewConnectionFilter(object subviewInstance)
		{
			object model = Traverse.Create(subviewInstance).Field("Model").GetValue();
			if (model == null)
			{
				return null;
			}

			FieldInfo fieldInfo = AccessTools.Field(model.GetType(), "connFilter");
			if (fieldInfo != null)
			{
				return fieldInfo.GetValue(model);
			}

			PropertyInfo propertyInfo = AccessTools.Property(model.GetType(), "connFilter");
			return propertyInfo?.GetValue(model, null);
		}

		private static string ResolveConnectionCardLabel(object subviewInstance, object data)
		{
			try
			{
				MethodInfo getInfo = subviewInstance?.GetType().GetMethod("GetPeepInfoForCard", BindingFlags.Instance | BindingFlags.NonPublic);
				return getInfo?.Invoke(subviewInstance, new[] { data }) as string;
			}
			catch
			{
				return null;
			}
		}

		private static void EnsureConnectionCardPortrait(GameObject card, object data)
		{
			if (card == null || !card.activeInHierarchy || !IsRenderableConnectionCard(data))
			{
				return;
			}

			if (!GetBoolField(data, "isKnownByHuman") || !GetBoolField(data, "isAlive") || !GetBoolField(data, "isAdult"))
			{
				return;
			}

			Entity peep = GetCardPeep(data);
			Sprite sprite = HUDUtil.GetCrewSprite(peep);
			Image image = card.transform.Find("Portrait/Portrait")?.GetComponent<Image>();
			if (image == null || sprite == null)
			{
				return;
			}

			image.sprite = sprite;
			image.color = Color.white;
			SetChildActive(card, "Portrait", true);
		}

		private static bool GetBoolField(object instance, string fieldName)
		{
			if (instance == null)
			{
				return false;
			}

			object value = AccessTools.Field(instance.GetType(), fieldName)?.GetValue(instance);
			return value is bool flag && flag;
		}

		private static void ClearCrewPickVisuals(GameObject pickGo)
		{
			if (pickGo == null)
			{
				return;
			}

			ClearImage(pickGo, "Button/Portrait");
			ClearText(pickGo, "Button/Text");
			SetChildActive(pickGo, "Warn Border", false);
			SetChildActive(pickGo, "Bar Panel", false);
			SetChildActive(pickGo, "State Icon", false);
		}

		private static GameObject ResolveGameObjectFromBasePick(object pick)
		{
			if (pick == null)
			{
				return null;
			}

			if (_basePickGoField == null)
			{
				Type type = pick.GetType();
				while (type != null && _basePickGoField == null)
				{
					_basePickGoField = type.GetField("go", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					type = type.BaseType;
				}
			}

			return _basePickGoField?.GetValue(pick) as GameObject;
		}

		private static GameObject ResolvePopupRoot(object instance)
		{
			if (instance == null)
			{
				return null;
			}

			Type instanceType = instance.GetType();
			if (!GoFieldsByType.TryGetValue(instanceType, out FieldInfo goField))
			{
				Type type = instanceType;
				while (type != null && goField == null)
				{
					goField = type.GetField("_go", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					type = type.BaseType;
				}
				GoFieldsByType[instanceType] = goField;
			}

			return goField?.GetValue(instance) as GameObject;
		}

		private static void SetTextValue(GameObject root, string path, string value)
		{
			Transform target = root?.transform.Find(path);
			if (target == null)
			{
				return;
			}

			TextMeshProUGUI tmp = target.GetComponent<TextMeshProUGUI>();
			if (tmp != null)
			{
				tmp.text = value;
			}

			Text text = target.GetComponent<Text>();
			if (text != null)
			{
				text.text = value;
			}
		}

		private static void ClearImage(GameObject root, string path)
		{
			Image image = root?.transform.Find(path)?.GetComponent<Image>();
			if (image != null)
			{
				image.sprite = null;
				image.color = Color.white;
			}
		}

		private static void ClearText(GameObject root, string path)
		{
			Transform target = root?.transform.Find(path);
			if (target == null)
			{
				return;
			}

			TextMeshProUGUI tmp = target.GetComponent<TextMeshProUGUI>();
			if (tmp != null)
			{
				tmp.text = string.Empty;
			}

			Text text = target.GetComponent<Text>();
			if (text != null)
			{
				text.text = string.Empty;
			}
		}

		private static void SetChildActive(GameObject root, string path, bool active)
		{
			Transform child = root?.transform.Find(path);
			if (child != null)
			{
				child.gameObject.SetActive(active);
			}
		}

		private static void LogSuppressOnce(string key, string message)
		{
			if (SuppressLogs.Add(key))
			{
				AfterProhibitionUIPlugin.Log?.LogInfo(message);
			}
		}
	}
}
