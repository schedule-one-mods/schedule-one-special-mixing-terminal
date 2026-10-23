using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Effects;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Product;
using MelonLoader;

namespace SpecialMixingTerminal;

internal sealed class MixAnalysis
{
    internal List<RecipePlan> Plans = [];
    internal List<MixStagePolicy> Stages = [];
    internal List<string> Effects = [];
    internal string Error = "";
    internal string BaseProductId = "";
    internal string FinalProduct = "";
    internal bool HasUnknown;
    internal RecipePlan Root => Plans[0];
}

internal static class MixAutomation
{
    private static readonly Dictionary<string, string> Failures = new();
    internal static (List<RecipePlan> Plans, string Error) Chain(TerminalConfigurable owner)
        => MixChain.Resolve(Mod.Terminals.Where(t => t != null && !t.IsDestroyed).Select(t => t.Plan), owner.Plan);
    internal static string LibraryPath(TerminalConfigurable owner) => Path.Combine(Path.GetDirectoryName(owner.Path)!, "library.json");
    internal static string Error(TerminalConfigurable owner)
    {
        foreach (var id in owner.Plan.AssignedMixerIds) if (Failures.TryGetValue(id, out var error)) return error;
        return "";
    }
    internal static void Clear(TerminalConfigurable owner)
    {
        foreach (var id in owner.Plan.AssignedMixerIds) Failures.Remove(id);
    }
    internal static void Clear() => Failures.Clear();

    private static ProductDefinition? InputProduct(RecipePlan root)
    {
        if (!string.IsNullOrEmpty(root.BaseProductId)) return Registry.GetItem(root.BaseProductId)?.TryCast<ProductDefinition>();
        if (root.NativeJobsOwned && !string.IsNullOrEmpty(root.NativeBaseProductId)) return Registry.GetItem(root.NativeBaseProductId)?.TryCast<ProductDefinition>();
        var owner = Mod.Terminals.FirstOrDefault(t => t.Plan == root);
        var slots = owner?.FindItem(root.InputShelfId)?.TryCast<ITransitEntity>()?.OutputSlots;
        ProductDefinition? found = null;
        if (slots == null) return null;
        for (var i = 0; i < slots.Count; i++)
        {
            var product = slots[i]?.ItemInstance?.Definition?.TryCast<ProductDefinition>();
            if (product == null) continue;
            if (found != null && found.ID != product.ID) return null;
            found = product;
        }
        return found;
    }

    internal static MixAnalysis Analyze(TerminalConfigurable owner)
    {
        var result = new MixAnalysis();
        var chain = Chain(owner); result.Plans = chain.Plans;
        if (chain.Error != "") { result.Error = chain.Error; return result; }
        foreach (var plan in result.Plans)
        {
            var error = plan.ValidateMixerLayout();
            if (error != "") { result.Error = "Complete every terminal in this chain: " + error; return result; }
        }
        var root = result.Root;
        var product = InputProduct(root);
        if (product == null) { result.Error = "Choose a base product, or stock the first product shelf with exactly one product type."; return result; }
        result.BaseProductId = product.ID;
        var start = root.FirstStageNumber;
        var end = start + result.Plans.Sum(p => p.Steps.Count) - 1;
        var final = root.FinalStageNumber == 0 ? end : root.FinalStageNumber;
        if (final < end || end > 9999) { result.Error = "The final step number must cover the entire connected chain."; return result; }
        var effects = new Il2CppSystem.Collections.Generic.List<Effect>();
        for (var i = 0; i < product.Properties.Count; i++) effects.Add(product.Properties[i]);
        var manager = NetworkSingleton<ProductManager>.Instance;
        var stage = start;
        foreach (var plan in result.Plans)
        for (var i = 0; i < plan.Steps.Count; i++, stage++)
        {
            var ingredient = Registry.GetItem(plan.Steps[i].IngredientId)?.TryCast<PropertyItemDefinition>();
            if (ingredient == null || ingredient.Properties.Count == 0) { result.Error = "An ingredient is unavailable or has no native mixing effect."; return result; }
            effects = EffectMixCalculator.MixProperties(effects, ingredient.Properties[0], product.DrugType);
            var known = manager.GetKnownProduct(product.DrugType, effects);
            var name = known != null ? known.Name : MixChain.ProductName(root.FinalProductName, stage, final);
            if (known == null) result.HasUnknown = true;
            var ids = new List<string>(); result.Effects.Clear();
            for (var e = 0; e < effects.Count; e++) { ids.Add(effects[e].ID); result.Effects.Add(effects[e].Name); }
            result.Stages.Add(new MixStagePolicy { MixerId = plan.ResolveMixerId(i), IngredientId = ingredient.ID, Stage = stage, ProductName = name, KnownProductId = known?.ID ?? "", DrugType = (int)product.DrugType, Effects = ids.OrderBy(x => x).ToList() });
            result.FinalProduct = known != null ? known.Name : root.FinalProductName == "" ? "Unknown product · choose a final name" : name;
            if (known == null && root.FinalProductName != "")
            {
                if (!ProductManager.IsMixNameValid(name)) { result.Error = "Choose a valid final product name."; return result; }
                if (NameOccupied(manager, name)) { result.Error = $"'{name}' belongs to a different product. Choose another final name."; return result; }
            }
        }
        return result;
    }

    private static bool NameOccupied(ProductManager manager, string name)
    {
        var id = ProductManager.MakeIDFileSafe(name.ToLowerInvariant().Replace(" ", ""));
        if (Registry.GetItem(id) != null) return true;
        for (var i = 0; i < manager.AllProducts.Count; i++)
            if (string.Equals(manager.AllProducts[i].Name, name, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    internal static string Prepare(TerminalConfigurable owner, out List<MixStagePolicy> policies, out string baseProductId)
    {
        policies = [];
        baseProductId = "";
        var chain = Chain(owner);
        if (chain.Error != "") return chain.Error;
        if (!chain.Plans[0].AutoAcceptMixes) { baseProductId = InputProduct(chain.Plans[0])?.ID ?? ""; return ""; }
        var analysis = Analyze(owner);
        if (analysis.Error != "") return analysis.Error;
        if (analysis.HasUnknown && string.IsNullOrWhiteSpace(analysis.Root.FinalProductName)) return "This chain has unknown results. Choose its final product name before Start.";
        baseProductId = analysis.BaseProductId;
        policies = analysis.Stages.Where(s => owner.Plan.AssignedMixerIds.Contains(s.MixerId)).ToList();
        return "";
    }

    // Harmony hooks are event-driven and only touch terminal-owned, completed
    // operations. No frame polling, global product renames or fake output items.
    internal static void BeforeMixingDone(MixingStation __instance) => Accept(__instance, true);
    internal static void BeforeCreateOutput(MixingStation __instance) => Accept(__instance, false);
    private static void Accept(MixingStation station, bool completionCallback)
    {
        if (!station.IsServerInitialized || station.CurrentMixOperation == null || !completionCallback && !station.IsMixingDone) return;
        var id = station.GUID.ToString();
        var owner = Mod.Terminals.FirstOrDefault(t => t != null && !t.IsDestroyed && t.Plan.NativeJobsOwned && t.Plan.AssignedMixerIds.Contains(id));
        var policy = owner?.Plan.NativeMixPolicies.FirstOrDefault(p => p.MixerId == id);
        if (owner == null || policy == null || Failures.ContainsKey(id)) return;
        try
        {
            var operation = station.CurrentMixOperation;
            if (operation.IsOutputKnown(out _)) return; // Preserve every existing name.
            var type = operation.GetOutput(out var effects);
            var ids = new List<string>();
            for (var i = 0; i < effects.Count; i++) ids.Add(effects[i].ID);
            if (operation.IngredientID != policy.IngredientId || (int)type != policy.DrugType || !ids.OrderBy(x => x).SequenceEqual(policy.Effects))
                throw new InvalidOperationException("Actual mixer input differs from this recipe. Stop and check the base product and ingredient routes.");
            var manager = NetworkSingleton<ProductManager>.Instance;
            if (NameOccupied(manager, policy.ProductName)) throw new InvalidOperationException("The planned product name is now occupied. Stop and choose another final name.");
            manager.FinishAndNameMix(operation.ProductID, operation.IngredientID, policy.ProductName);
            if (!operation.IsOutputKnown(out _)) throw new InvalidOperationException("Native product registration did not complete. Stop this terminal and inspect the loader log.");
            MelonLogger.Msg($"[Mixing terminal {owner.Station.GUID}] Accepted stage {policy.Stage}: {policy.ProductName}");
        }
        catch (Exception error)
        {
            Failures[id] = error.Message;
            MelonLogger.Error($"[Mixing terminal {owner.Station.GUID}] Automatic acceptance paused: {error}");
        }
    }
}
