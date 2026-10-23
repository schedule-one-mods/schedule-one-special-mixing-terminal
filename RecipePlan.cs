using System.Text.Json;

namespace SpecialMixingTerminal;

/// <summary>One ordered instruction sent to a handler: source ingredient, destination mixer, and stack cap.</summary>
public sealed class IngredientStep
{
    public string IngredientId { get; set; } = "";
    public string SupplyShelfId { get; set; } = "";
    public string MixerId { get; set; } = "";
    public int Threshold { get; set; } = 20;
}

public sealed class RecipePlan
{
    public const int EquipmentLimit = 4;
    public string Name { get; set; } = "Special mix";
    public string PropertyCode { get; set; } = "";
    public string AssignedHandlerId { get; set; } = "";
    public string AssignedChemistId { get; set; } = "";
    public List<string> AssignedChemistIds { get; set; } = [];
    public List<string> AssignedHandlerIds { get; set; } = [];
    public List<string> AssignedMixerIds { get; set; } = [];
    public List<string> IngredientShelfIds { get; set; } = [];
    public string BaseProductId { get; set; } = "";
    public string InputShelfId { get; set; } = "";
    public string OutputShelfId { get; set; } = "";
    public List<IngredientStep> Steps { get; set; } = [];
    public List<NativeRouteBinding> NativeRoutes { get; set; } = [];
    public Dictionary<string, List<string>> NativeChemistStations { get; set; } = [];
    public bool NativeJobsOwned { get; set; }
    public int NativeStep { get; set; }
    public int NativePhase { get; set; }
    public string NativeProduct { get; set; } = "";
    public string NativeOutput { get; set; } = "";

    public void Normalize()
    {
        Steps ??= [];
        AssignedChemistIds ??= [];
        AssignedHandlerIds ??= [];
        AssignedMixerIds ??= [];
        IngredientShelfIds ??= [];
        NativeRoutes ??= [];
        NativeChemistStations ??= [];
        AssignedMixerIds = AssignedMixerIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().Take(EquipmentLimit).ToList();
        IngredientShelfIds = IngredientShelfIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().Take(EquipmentLimit).ToList();
        if (AssignedHandlerIds.Count == 0 && !string.IsNullOrEmpty(AssignedHandlerId)) AssignedHandlerIds.Add(AssignedHandlerId);
        if (AssignedChemistIds.Count == 0 && !string.IsNullOrEmpty(AssignedChemistId)) AssignedChemistIds.Add(AssignedChemistId);
        AssignedHandlerId = ""; AssignedChemistId = "";
        AssignedHandlerIds = AssignedHandlerIds.Distinct().Take(1).ToList();
        AssignedChemistIds = AssignedChemistIds.Distinct().Take(1).ToList();
        foreach (var step in Steps)
            step.Threshold = 20;
    }

    public string ResolveMixerId(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= Steps.Count || AssignedMixerIds.Count == 0) return "";
        var requested = Steps[stepIndex].MixerId;
        return !string.IsNullOrEmpty(requested) && AssignedMixerIds.Contains(requested)
            ? requested : stepIndex < AssignedMixerIds.Count ? AssignedMixerIds[stepIndex] : "";
    }

    public string ResolveSupplyId(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= Steps.Count) return "";
        var requested = Steps[stepIndex].SupplyShelfId;
        return IngredientShelfIds.Contains(requested) ? requested : IngredientShelfIds.Count == 1 ? IngredientShelfIds[0] : "";
    }

    public static RecipePlan Load(string path)
    {
        if (!File.Exists(path)) return new RecipePlan();
        try
        {
            var plan = JsonSerializer.Deserialize<RecipePlan>(File.ReadAllText(path)) ?? new RecipePlan();
            plan.Normalize();
            return plan;
        }
        catch { return new RecipePlan(); }
    }

    public void Save(string path)
    {
        Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var pending = path + ".tmp";
        File.WriteAllText(pending, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(pending, path, true);
    }
}

public sealed class NativeRouteBinding
{
    public string HandlerId { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string DestinationId { get; set; } = "";
}
