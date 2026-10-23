using Il2CppScheduleOne;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.ObjectScripts;

namespace SpecialMixingTerminal;

internal static class MixerWiring
{
    internal static bool SupportsOutput(BuildableItem? item)
        => item != null && item.TryCast<ITransitEntity>() != null &&
            (item.TryCast<PlaceableStorageEntity>() != null || item.TryCast<PackagingStation>() != null || item.TryCast<BrickPress>() != null);

    internal static string Validate(TerminalConfigurable owner)
    {
        var plan = owner.Plan;
        var layoutError = plan.ValidateMixerLayout();
        if (layoutError != "") return layoutError;
        if (plan.BaseProductId != "" && Registry.GetItem(plan.BaseProductId)?.TryCast<Il2CppScheduleOne.Product.ProductDefinition>() == null) return "Choose an available base product.";
        if (owner.FindItem(plan.InputShelfId)?.TryCast<PlaceableStorageEntity>() == null) return "Assign a product supply shelf.";
        if (!SupportsOutput(owner.FindItem(plan.OutputShelfId))) return "Assign an output shelf, packaging station or brick press.";
        if (plan.InputShelfId == plan.OutputShelfId) return "Use different product and output shelves.";
        if (plan.AssignedMixerIds.Any(id => owner.FindItem(id)?.TryCast<MixingStation>() == null)) return "An assigned mixer is missing.";
        for (var i = 0; i < plan.Steps.Count; i++)
        {
            if (owner.FindItem(plan.ResolveMixerId(i))?.TryCast<MixingStation>() == null) return $"Choose the mixer for ingredient {i + 1}.";
            if (owner.FindItem(plan.ResolveSupplyId(i))?.TryCast<PlaceableStorageEntity>() == null) return $"Choose the supply shelf for ingredient {i + 1}.";
            if (Registry.GetItem(plan.Steps[i].IngredientId) == null) return $"Choose ingredient {i + 1}.";
        }
        return "";
    }

    internal static string Apply(TerminalConfigurable owner)
    {
        var error = Validate(owner);
        if (error != "") return error;
        var plan = owner.Plan;
        // Validate the whole chain before writing any native station fields.
        foreach (var id in plan.AssignedMixerIds)
        {
            var station = owner.FindItem(id)!.Cast<MixingStation>();
            var index = Enumerable.Range(0, plan.Steps.Count).FirstOrDefault(i => plan.ResolveMixerId(i) == id, -1);
            station.stationConfiguration.StartThrehold.SetValue(20, true);
            var filter = new SlotFilter { Type = SlotFilter.EType.Whitelist };
            if (index >= 0) filter.ItemIDs.Add(plan.Steps[index].IngredientId);
            station.SetSlotFilter(null!, station.MixerSlot.SlotIndex, filter);
            var upstream = Mod.Terminals.Any(t => t != owner && !t.IsDestroyed && t.Plan.OutputShelfId != "" && t.Plan.OutputShelfId == plan.InputShelfId);
            var productFilter = new SlotFilter { Type = index == 0 && !upstream && plan.BaseProductId != "" ? SlotFilter.EType.Whitelist : SlotFilter.EType.Blacklist };
            if (productFilter.Type == SlotFilter.EType.Whitelist)
                productFilter.ItemIDs.Add(plan.BaseProductId);
            station.SetSlotFilter(null!, station.ProductSlot.SlotIndex, productFilter);
            var destination = index < 0 ? null : owner.FindItem(plan.ResolveDestinationId(index));
            station.stationConfiguration.Destination.SetObject(destination!, true);
        }
        return "";
    }
}
