using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Shop;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using Il2CppScheduleOne.Interaction;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.Tools;
using Il2CppInterop.Runtime.Injection;

[assembly: MelonInfo(typeof(SpecialMixingTerminal.Mod), "Special Mixing Terminal", "0.5.2", "Dekode1859")]
[assembly: MelonGame("TVGS", "Schedule I")]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.IL2CPP)]

namespace SpecialMixingTerminal;

public sealed class Mod : MelonMod
{
    private static Mod? _self;
    private const string TerminalId = "specialmixingterminal";
    private bool _registered;
    private float _next;
    private SaveManager? _saveManager;
    private UnityAction? _saving, _saved;
    private readonly Dictionary<string, string> _saveSnapshot = new();

    public override void OnInitializeMelon()
    {
        ClassInjector.RegisterTypeInIl2Cpp<TerminalConfigurable>(new RegisterTypeOptions { Interfaces = new[] { typeof(IConfigurable) } });
        LoggerInstance.Msg("Special Mixing Terminal initializing against native Schedule I IL2CPP classes.");
    }

    public override void OnLateInitializeMelon()
    {
        _self = this;
        HarmonyInstance.Patch(AccessTools.Method(typeof(LaunderingInterface), nameof(LaunderingInterface.Interacted)),
            prefix: new HarmonyMethod(typeof(Mod), nameof(BeforeLaunderingInteracted)));
        HarmonyInstance.Patch(AccessTools.Method(typeof(LaunderingInterface), nameof(LaunderingInterface.Initialize)),
            prefix: new HarmonyMethod(typeof(Mod), nameof(BeforeLaunderingInitialize)));
        HarmonyInstance.Patch(AccessTools.Method(typeof(LaunderingStation), nameof(LaunderingStation.CanBeDestroyed)),
            prefix: new HarmonyMethod(typeof(Mod), nameof(BeforeTerminalPickup)));
        HarmonyInstance.Patch(AccessTools.Method(typeof(Registry), nameof(Registry.Awake)),
            postfix: new HarmonyMethod(typeof(Mod), nameof(AfterRegistryAwake)));
        HarmonyInstance.Patch(AccessTools.Method(typeof(Registry), nameof(Registry.RemoveRuntimeItems)),
            postfix: new HarmonyMethod(typeof(Mod), nameof(AfterRegistryAwake)));
        foreach (var method in new[] { "RefreshLaunderButton", "UpdateTimeline", "UpdateCurrentTotal" })
            HarmonyInstance.Patch(AccessTools.Method(typeof(LaunderingInterface), method),
                prefix: new HarmonyMethod(typeof(Mod), nameof(BeforeNativeLaunderingRefresh)));
        HarmonyInstance.Patch(AccessTools.Method(typeof(ManagementInterface), nameof(ManagementInterface.InitializeConfigPanel)),
            prefix: new HarmonyMethod(typeof(TerminalClipboard), nameof(TerminalClipboard.BeforeInitializePanel)));
        HarmonyInstance.Patch(AccessTools.Method(typeof(ConfigurableType), nameof(ConfigurableType.GetTypeName)),
            prefix: new HarmonyMethod(typeof(TerminalClipboard), nameof(TerminalClipboard.BeforeGetTypeName)));
        HarmonyInstance.Patch(AccessTools.Method(typeof(EntityConfiguration), nameof(EntityConfiguration.ReplicateField)),
            prefix: new HarmonyMethod(typeof(TerminalClipboard), nameof(TerminalClipboard.BeforeReplicateField)));
    }

    private static bool BeforeNativeLaunderingRefresh(LaunderingInterface __instance)
        => !UnityEngine.Object.FindObjectsOfType<LaunderingStation>(true)
            .Any(station => station != null && station.Interface == __instance && station.ItemInstance?.ID == TerminalId);

    private static bool ClipboardEquipped => Singleton<ManagementClipboard>.InstanceExists && Singleton<ManagementClipboard>.Instance.IsEquipped;

    private static bool BeforeTerminalPickup(LaunderingStation __instance, ref string reason, ref bool __result)
    {
        if (__instance.ItemInstance?.ID != TerminalId) return true;
        var owner = __instance.GetComponent<TerminalConfigurable>();
        __result = owner?.Plan.NativeJobsOwned != true && !__instance.Interface.IsOpen;
        reason = __result ? "" : "Close the computer and stop its employee jobs before picking it up.";
        return false;
    }

    private static void AfterRegistryAwake(Registry __instance)
    {
        try { _self?.RegisterDefinition(__instance); }
        catch (Exception e) { _self?.LoggerInstance.Error("Terminal definition registration failed: " + e); }
    }

    private BuildableItemDefinition RegisterDefinition(Registry registry)
    {
        var definition = Registry.GetItem(TerminalId)?.TryCast<BuildableItemDefinition>();
        if (definition != null) return definition;
        var source = Registry.GetItem("launderingstation")?.TryCast<BuildableItemDefinition>();
        if (source == null || source.BuiltItem == null) throw new InvalidOperationException("The native laundering terminal buildable is unavailable.");
        definition = UnityEngine.Object.Instantiate(source);
        definition.name = "Special Mixing Terminal Definition";
        definition.ID = TerminalId;
        definition.Name = "Special Mixing Terminal";
        definition.Description = "A programmable terminal for managing special product mixing.";
        definition.BasePurchasePrice = 6500f;
        definition.RequiresLevelToPurchase = false;
        definition.BuiltItem = source.BuiltItem;
        registry.AddToRegistry(definition);
        LoggerInstance.Msg($"Registered native buildable: {definition.ID} ({definition.Name}).");
        return definition;
    }

    private static bool BeforeLaunderingInitialize(LaunderingInterface __instance)
    {
        // Unity's stripped generic GetComponentInParent overload is not usable
        // through IL2CPP here. Compare the native interface references instead.
        var station = UnityEngine.Object.FindObjectsOfType<LaunderingStation>(true)
            .FirstOrDefault(candidate => candidate != null && candidate.Interface == __instance && candidate.ItemInstance?.ID == TerminalId);
        if (station == null || station.ItemInstance?.ID != TerminalId || _self == null) return true;
        // The original initializer requires a Business. Ordinary production properties
        // supply null, throwing before BuildManager activates or spawns the object.
        var target = __instance.intObj;
        if (target == null) throw new InvalidOperationException("Terminal prefab is missing its interactable object.");
        target.onHovered = new UnityEvent();
        target.onInteractStart = new UnityEvent();
        target.onInteractEnd = new UnityEvent();
        target.onHovered.AddListener((UnityAction)(() =>
        {
            target.SetMessage(ClipboardEquipped ? "" : "Use mixing terminal");
            target.SetInteractableState(ClipboardEquipped ? InteractableObject.EInteractableState.Disabled : InteractableObject.EInteractableState.Default);
        }));
        target.onInteractStart.AddListener((UnityAction)(() => _self.OpenTerminal(station)));
        target.SetMessage("Use mixing terminal");
        target.SetInteractionType(InteractableObject.EInteractionType.Key_Press);
        target.SetInteractableState(InteractableObject.EInteractableState.Default);
        AttachConfiguration(station);
        _self.LoggerInstance.Msg($"Initialized mixing terminal {station.GUID} at {station.ParentProperty?.PropertyName}; bypassed business-only laundering setup.");
        return false;
    }

    private bool RegisterTerminal()
    {
        var registry = Singleton<Registry>.Instance;
        var definition = RegisterDefinition(registry);

        var shops = UnityEngine.Object.FindObjectsOfType<ShopInterface>(true);
        var oscar = shops.FirstOrDefault(shop => shop != null && shop.ShopCode == "shop");
        if (oscar == null) return false;
        for (var i = 0; i < oscar.Listings.Count; i++)
        {
            if (oscar.Listings[i]?.Item?.ID == TerminalId) return true;
        }
        var listing = new ShopListing
        {
            name = "Special Mixing Terminal",
            Item = definition,
            OverridePrice = true,
            OverriddenPrice = 6500f,
            LimitedStock = false,
            CanBeDelivered = false,
            TieStockToNumberVariable = false,
            TrackPurchases = false,
            ConditionalVisibility = false,
            EnforceMinimumGameCreationVersion = false,
            Shop = oscar
        };
        oscar.Listings.Add(listing);
        oscar.CreateListingUI(listing);
        oscar.RefreshShownItems();
        LoggerInstance.Msg($"Added {TerminalId} to {oscar.ShopName} ({oscar.ShopCode}) at $6,500.");
        return true;
    }

    private static bool BeforeLaunderingInteracted(LaunderingInterface __instance)
    {
        if (_self == null) return true;
        try
        {
            var station = UnityEngine.Object.FindObjectsOfType<LaunderingStation>(true)
                .FirstOrDefault(candidate => candidate != null && candidate.Interface == __instance && candidate.ItemInstance?.ID == TerminalId);
            if (station == null) return true;
            _self.OpenTerminal(station);
            return false;
        }
        catch (Exception e)
        {
            _self.LoggerInstance.Error("Could not open Special Mixing Terminal: " + e);
            return true;
        }
    }

    private readonly Dictionary<string, TerminalScreen> _screens = new();

    private static TerminalConfigurable AttachConfiguration(LaunderingStation station)
    {
        var config = station.GetComponent<TerminalConfigurable>() ?? station.gameObject.AddComponent<TerminalConfigurable>();
        config.Initialize(station);
        config.RegisterWithProperty();
        return config;
    }

    private void OpenTerminal(LaunderingStation station)
    {
        if (ClipboardEquipped) return;
        var owner = AttachConfiguration(station);
        var id = station.GUID.ToString();
        if (!_screens.TryGetValue(id, out var screen))
        {
            screen = new TerminalScreen(station, PlanPath(station), LoggerInstance, owner.Plan);
            _screens[id] = screen;
        }
        screen.Open();
    }
    public override void OnUpdate()
    {
        NativeJobs.Tick();
        foreach (var owner in UnityEngine.Object.FindObjectsOfType<TerminalConfigurable>())
            if (!owner.IsDestroyed)
            {
                var interaction = owner.Station.Interface.intObj;
                interaction.enabled = !ClipboardEquipped;
                if (ClipboardEquipped) interaction.SetInteractableState(InteractableObject.EInteractableState.Disabled);
            }
        foreach (var screen in _screens.Values) screen.Tick();
        if (Time.realtimeSinceStartup < _next || !Singleton<LoadManager>.InstanceExists || !Singleton<LoadManager>.Instance.IsGameLoaded || !Singleton<Registry>.InstanceExists) return;
        _next = Time.realtimeSinceStartup + 1;
        try
        {
            if (_saveManager == null && Singleton<SaveManager>.InstanceExists)
            {
                _saveManager = Singleton<SaveManager>.Instance;
                _saving = (UnityAction)CaptureRecipesForSave;
                _saved = (UnityAction)RestoreRecipesAfterSave;
                _saveManager.onSaveStart.AddListener(_saving);
                _saveManager.onSaveComplete.AddListener(_saved);
            }
            if (!_registered) _registered = RegisterTerminal();
            foreach (var station in UnityEngine.Object.FindObjectsOfType<LaunderingStation>(true))
                if (station.ItemInstance?.ID == TerminalId && !station.isGhost) AttachConfiguration(station);
        }
        catch (Exception e) { LoggerInstance.Error("Could not register Special Mixing Terminal: " + e); }
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        if (sceneName != "Main") return;
        _registered = false;
        foreach (var screen in _screens.Values) screen.Dispose();
        _screens.Clear();
        NativeJobs.Clear();
        if (_saveManager != null)
        {
            if (_saving != null) _saveManager.onSaveStart.RemoveListener(_saving);
            if (_saved != null) _saveManager.onSaveComplete.RemoveListener(_saved);
        }
        _saveManager = null;
        _saveSnapshot.Clear();
    }

    private void CaptureRecipesForSave()
    {
        NativeJobs.BeforeSave();
        _saveSnapshot.Clear();
        foreach (var owner in UnityEngine.Object.FindObjectsOfType<TerminalConfigurable>(true))
            if (owner.Plan != null && !owner.IsDestroyed)
                _saveSnapshot[owner.Path] = System.Text.Json.JsonSerializer.Serialize(owner.Plan, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    private void RestoreRecipesAfterSave()
    {
        NativeJobs.AfterSave();
        if (SaveManager.SaveError) return;
        foreach (var entry in _saveSnapshot)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(entry.Key)!);
            File.WriteAllText(entry.Key, entry.Value);
        }
        _saveSnapshot.Clear();
    }

    internal static string PlanPath(LaunderingStation station)
    {
        var code = station.ParentProperty?.PropertyCode ?? "unknown";
        return Path.Combine(Singleton<LoadManager>.Instance.LoadedGameFolderPath,
            "ModData", "SpecialMixingTerminal", code + "-" + station.GUID + ".json");
    }
}
