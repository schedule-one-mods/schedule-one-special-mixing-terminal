using Il2CppFishNet.Connection;
using Il2CppFishNet.Object;
using Il2CppInterop.Runtime.Attributes;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Packaging;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.UI.Management;
using UnityEngine;
using UnityEngine.Events;
using Il2CppInterop.Runtime;
using NativeItems = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.EntityFramework.BuildableItem>;

namespace SpecialMixingTerminal;

// Registered with IConfigurable so the normal clipboard discovers the terminal
// through its collider and the property's existing configurable registry.
public sealed class TerminalConfigurable : MonoBehaviour
{
    public TerminalConfigurable(IntPtr pointer) : base(pointer) { }
    internal const EConfigurableType TerminalType = (EConfigurableType)1000;
    internal LaunderingStation Station = null!;
    internal RecipePlan Plan = null!;
    internal string Path = "";
    internal ObjectField ProductSupply = null!;
    internal ObjectField OutputShelf = null!;
    internal ObjectListField Mixers = null!;
    internal ObjectListField IngredientSupplies = null!;
    private bool _syncing;
    private EntityConfiguration _config = null!;
    private ConfigurationReplicator _replicator = null!;
    private bool _ready;

    public EntityConfiguration Configuration => _config;
    public ConfigurationReplicator ConfigReplicator => _replicator;
    public EConfigurableType ConfigurableType => TerminalType;
    public WorldspaceUIElement WorldspaceUI { get; set; } = null!;
    public NetworkObject CurrentPlayerConfigurer { get; set; } = null!;
    public bool IsBeingConfiguredByOtherPlayer => CurrentPlayerConfigurer != null && CurrentPlayerConfigurer != Player.Local?.NetworkObject;
    public Sprite TypeIcon => Station.ItemInstance.Definition.Icon;
    public Transform Transform => transform;
    public Transform UIPoint => Station.MidAirCenterPoint ?? transform;
    public bool IsDestroyed => Station == null || Station.IsDestroyed;
    public bool CanBeSelected => _ready && !IsDestroyed && Station.gameObject.activeInHierarchy && ParentProperty != null && ParentProperty.IsOwned;
    public Property ParentProperty => Station.ParentProperty;

    [HideFromIl2Cpp]
    internal void Initialize(LaunderingStation station)
    {
        if (_ready) return;
        Station = station;
        Path = Mod.PlanPath(station);
        Plan = RecipePlan.Load(Path);
        Plan.PropertyCode = station.ParentProperty.PropertyCode;
        // EntityConfiguration requires a replicator even for local fields. Keep
        // this holder inactive: terminal fields persist in the per-save JSON and
        // must not register new FishNet RPCs on the vanilla terminal prefab.
        var holder = new GameObject("Mixing terminal configuration");
        holder.SetActive(false);
        holder.transform.SetParent(station.transform, false);
        _replicator = holder.AddComponent<ConfigurationReplicator>();
        _config = new EntityConfiguration(_replicator, this.Cast<IConfigurable>(), "Special Mixing Terminal");
        ProductSupply = new ObjectField(_config);
        OutputShelf = new ObjectField(_config);
        Mixers = new ObjectListField(_config) { MaxItems = RecipePlan.EquipmentLimit };
        IngredientSupplies = new ObjectListField(_config) { MaxItems = RecipePlan.EquipmentLimit };
        ProductSupply.TypeRequirements.Add(Il2CppType.Of<PlaceableStorageEntity>());
        OutputShelf.TypeRequirements.Add(Il2CppType.Of<PlaceableStorageEntity>());
        OutputShelf.TypeRequirements.Add(Il2CppType.Of<PackagingStation>());
        OutputShelf.TypeRequirements.Add(Il2CppType.Of<PackagingStationMk2>());
        OutputShelf.TypeRequirements.Add(Il2CppType.Of<BrickPress>());
        Mixers.TypeRequirements.Add(Il2CppType.Of<MixingStation>());
        // The native selector compares concrete types, so Mk II needs its own
        // entry even though it derives from MixingStation.
        Mixers.TypeRequirements.Add(Il2CppType.Of<MixingStationMk2>());
        IngredientSupplies.TypeRequirements.Add(Il2CppType.Of<PlaceableStorageEntity>());
        ResetFieldCallbacks();
        _ready = true;
        RegisterWithProperty();
        var property = station.ParentProperty;
        station.onDestroyed.AddListener((UnityAction)(() =>
        {
            Mod.ForgetTerminal(this);
            if (property != null) property.Configurables.Remove(this.Cast<IConfigurable>());
            DestroyWorldspaceUI();
        }));
    }

    [HideFromIl2Cpp]
    internal void RegisterWithProperty()
    {
        if (!_ready || ParentProperty == null) return;
        var config = this.Cast<IConfigurable>();
        if (!ParentProperty.Configurables.Contains(config)) ParentProperty.Configurables.Add(config);
    }

    [HideFromIl2Cpp]
    internal void Save() => Plan.Save(Path);

    [HideFromIl2Cpp]
    internal void ResetFieldCallbacks()
    {
        // These fields belong exclusively to this terminal. Native Bind adds UI
        // refresh listeners; discard them when that panel is destroyed so old
        // clipboard panels and managed callbacks cannot accumulate.
        ProductSupply.onObjectChanged.RemoveAllListeners();
        OutputShelf.onObjectChanged.RemoveAllListeners();
        Mixers.onListChanged.RemoveAllListeners();
        IngredientSupplies.onListChanged.RemoveAllListeners();
        ProductSupply.onObjectChanged.AddListener((UnityAction<BuildableItem>)(item => { if (RejectChange()) return; Plan.InputShelfId = item == null ? "" : item.GUID.ToString(); Save(); }));
        OutputShelf.onObjectChanged.AddListener((UnityAction<BuildableItem>)(item => { if (RejectChange()) return; Plan.OutputShelfId = item == null ? "" : item.GUID.ToString(); Save(); }));
        Mixers.onListChanged.AddListener((UnityAction<NativeItems>)(items => { if (RejectChange()) return; Plan.AssignedMixerIds = Ids(items); Save(); }));
        IngredientSupplies.onListChanged.AddListener((UnityAction<NativeItems>)(items => { if (RejectChange()) return; Plan.IngredientShelfIds = Ids(items); Save(); }));
    }

    [HideFromIl2Cpp]
    private bool RejectChange()
    {
        if (_syncing) return true;
        if (!Plan.NativeJobsOwned) return false;
        RefreshFields(); return true;
    }

    [HideFromIl2Cpp]
    private static List<string> Ids(NativeItems items)
    {
        var ids = new List<string>();
        for (var i = 0; i < items.Count; i++) if (items[i] != null) ids.Add(items[i].GUID.ToString());
        return ids;
    }

    [HideFromIl2Cpp]
    internal void RefreshFields()
    {
        _syncing = true;
        try
        {
            ProductSupply.SetObject(FindItem(Plan.InputShelfId)!, false);
            OutputShelf.SetObject(FindItem(Plan.OutputShelfId)!, false);
            Mixers.SetList(ResolveItems(Plan.AssignedMixerIds), false);
            IngredientSupplies.SetList(ResolveItems(Plan.IngredientShelfIds), false);
        }
        finally { _syncing = false; }
    }

    [HideFromIl2Cpp]
    private NativeItems ResolveItems(List<string> ids)
    {
        var items = new NativeItems();
        foreach (var id in ids.Take(RecipePlan.EquipmentLimit)) { var item = FindItem(id); if (item != null) items.Add(item); }
        return items;
    }

    [HideFromIl2Cpp]
    internal BuildableItem? FindItem(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return null;
        var items = ParentProperty.BuildableItems;
        if (Time.realtimeSinceStartup >= _equipmentAt || items.Count != _equipmentCount)
        {
            _equipmentAt = Time.realtimeSinceStartup + 1f;
            _equipmentCount = items.Count;
            _equipment.Clear();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item != null && !item.IsDestroyed) _equipment[item.GUID.ToString()] = item;
            }
        }
        return _equipment.TryGetValue(guid, out var found) && found != null && !found.IsDestroyed && found.ParentProperty == ParentProperty ? found : null;
    }

    private readonly Dictionary<string, BuildableItem> _equipment = new();
    private float _equipmentAt;
    private int _equipmentCount = -1;

    public WorldspaceUIElement CreateWorldspaceUI()
    {
        if (WorldspaceUI != null) return WorldspaceUI;
        var template = UnityEngine.Object.Instantiate(Singleton<ManagementUtilities>.Instance.StorageUIElementPrefab,
            Singleton<ManagementWorldspaceCanvas>.Instance.Canvas.transform, false);
        // StorageUIElement.RefreshUI dereferences a PlaceableStorageEntity. The
        // terminal uses the same visuals with a base world-space element.
        var ui = template.gameObject.AddComponent<WorldspaceUIElement>();
        ui.RectTransform = template.GetComponent<RectTransform>();
        ui.Container = template.Container;
        ui.TitleLabel = template.TitleLabel;
        ui.AssignedWorkerDisplay = template.AssignedWorkerDisplay;
        ui.TitleLabel.text = "Special Mixing Terminal";
        template.Icon.sprite = TypeIcon;
        template.enabled = false;
        UnityEngine.Object.Destroy(template);
        if (ui.AssignedWorkerDisplay != null) ui.AssignedWorkerDisplay.gameObject.SetActive(false);
        WorldspaceUI = ui;
        ui.gameObject.SetActive(true);
        return ui;
    }

    public void DestroyWorldspaceUI()
    {
        if (WorldspaceUI != null) { WorldspaceUI.Destroy(); WorldspaceUI = null!; }
    }
    public void ShowOutline(Color color) => Station.ShowOutline(color);
    public void HideOutline() => Station.HideOutline();
    public void Selected() { if (_ready) _config.Selected(); }
    public void Deselected() { if (_ready) _config.Deselected(); }
    public void SetConfigurer(NetworkObject player) => CurrentPlayerConfigurer = player;
    public void SendConfigurationToClient(NetworkConnection conn) { }
}
