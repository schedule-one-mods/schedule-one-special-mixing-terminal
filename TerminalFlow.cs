using Il2CppScheduleOne;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppTMPro;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SpecialMixingTerminal;

internal sealed partial class TerminalScreen
{
    private sealed record ActualRoute(Employee Worker, string Source, string Destination, AdvancedTransitRoute Route);
    private static readonly Color LinkGood = new(.30f, .80f, .49f);
    private static readonly Color LinkBad = new(.94f, .40f, .30f);
    private readonly StringBuilder _flowKey = new(2048);

    private List<ActualRoute> ReadActualRoutes()
    {
        var result = new List<ActualRoute>();
        foreach (var worker in NativeJobs.Employees())
        {
            if (worker.AssignedProperty != _station.ParentProperty) continue;
            var config = worker.TryCast<IConfigurable>()?.Configuration?.TryCast<PackagerConfiguration>();
            if (config == null) continue;
            for (var i = 0; i < config.Routes.Routes.Count; i++)
            {
                var route = config.Routes.Routes[i];
                var destination = TransitItem(route.Destination)?.GUID.ToString() ?? "";
                if (!_plan.AssignedMixerIds.Contains(destination)) continue;
                result.Add(new ActualRoute(worker, TransitItem(route.Source)?.GUID.ToString() ?? "", destination, route));
            }
        }
        return result;
    }

    private static BuildableItem? TransitItem(ITransitEntity? entity)
        => entity?.TryCast<BuildableItem>() ?? entity?.TryCast<Component>()?.GetComponent<BuildableItem>();

    private static bool StationListed(Employee worker, BuildableItem station)
    {
        var list = worker.TryCast<IConfigurable>()?.Configuration?.TryCast<ChemistConfiguration>()?.Stations.SelectedObjects;
        if (list == null) return false;
        for (var i = 0; i < list.Count; i++) if (list[i]?.GUID.ToString() == station.GUID.ToString()) return true;
        return false;
    }

    private void BuildLiveFlow(RectTransform view)
    {
        var routes = ReadActualRoutes();
        var running = _plan.NativeJobsOwned;
        var handler = _plan.AssignedHandlerIds.Count == 1 ? NativeJobs.Employee(_plan.AssignedHandlerIds[0]) : null;
        var chemist = _plan.AssignedChemistIds.Count == 1 ? NativeJobs.Employee(_plan.AssignedChemistIds[0]) : null;
        var handlerError = NativeJobs.Ready(Owner, EEmployeeType.Handler);
        var chemistError = NativeJobs.Ready(Owner, EEmployeeType.Chemist);
        var handlerReady = !running && handlerError == "";
        var chemistReady = !running && chemistError == "";
        var ids = Enumerable.Range(0, Math.Min(4, _plan.Steps.Count)).Select(_plan.ResolveMixerId)
            .Concat(_plan.AssignedMixerIds).Where(id => id != "").Distinct().Take(4).ToList();
        var key = _flowKey.Clear();
        foreach (var route in routes)
        {
            key.Append('|').Append(route.Worker.GUID).Append(':').Append(route.Source).Append(':').Append(route.Destination).Append(':').Append(route.Route.Filter.Mode);
            var items = route.Route.Filter.Items;
            for (var i = 0; i < items.Count; i++) key.Append(':').Append(items[i]?.ID);
        }
        key.Append('|').Append(running).Append(':').Append(handler?.GUID).Append(':').Append(chemist?.GUID).Append(':').Append(handlerError).Append(':').Append(chemistError);
        foreach (var id in ids)
        {
            var station = Owner.FindItem(id)?.TryCast<MixingStation>();
            if (station == null) { key.Append("|missing:").Append(id); continue; }
            var config = station.stationConfiguration;
            var filter = station.MixerSlot.PlayerFilter;
            var worker = config.AssignedChemist.SelectedNPC?.TryCast<Employee>();
            key.Append('|').Append(id).Append(':').Append(EquipmentName(id)).Append(':').Append(config.StartThrehold.Value).Append(':').Append(config.Destination.SelectedObject?.GUID).Append(':').Append(worker?.GUID);
            key.Append(':').Append(worker != null && StationListed(worker, station)).Append(':').Append(filter?.Type);
            if (filter != null) for (var i = 0; i < filter.ItemIDs.Count; i++) key.Append(':').Append(filter.ItemIDs[i]);
        }
        key.Append('|').Append(_plan.InputShelfId).Append(':').Append(_plan.OutputShelfId).Append(':').Append(_plan.Steps.Count);
        for (var i = 0; i < _plan.Steps.Count; i++) key.Append('|').Append(_plan.Steps[i].IngredientId).Append(':').Append(EquipmentName(_plan.ResolveSupplyId(i)));
        key.Append('|').Append(EquipmentName(_plan.InputShelfId)).Append('|').Append(EquipmentName(_plan.OutputShelfId));
        var signature = key.ToString();
        if (_flowSignature == signature) return;
        _flowSignature = signature;
        Clear(view);
        Label(view, running ? "LIVE EQUIPMENT LINKS" : "ROUTE READINESS", 0, 0, 1, .055f, .019f, style: FontStyles.Bold);
        Label(view, running ? "Actual native jobs | Missing links shown in red" : "Green: ready for Start | Red: needs attention", 0, .065f, 1, .055f, .015f);
        var feed = routes.FirstOrDefault(r => r.Source == _plan.InputShelfId && r.Destination == _plan.ResolveMixerId(0));
        var feedGood = feed != null || handlerReady;
        var product = Panel("Product supply", view, 0, .84f, .23f, .13f, new Color(.16f, .24f, .31f));
        Label(product.transform, "PRODUCT", .04f, .03f, .92f, .30f, .014f, style: FontStyles.Bold, alignment: TextAlignmentOptions.Center);
        Label(product.transform, EquipmentName(_plan.InputShelfId, "Not assigned"), .04f, .38f, .92f, .55f, .016f, feedGood ? LinkGood : LinkBad, alignment: TextAlignmentOptions.Center);
        FlowArrow(view, .035f, .835f, .035f, .675f, feedGood ? LinkGood : LinkBad);
        WorkerBadge(view, feed?.Worker ?? (handlerReady ? handler : null), .055f, .765f, HandlerColor);
        for (var column = 0; column < 4; column++)
        {
            var x = column * .255f;
            var id = column < ids.Count ? ids[column] : "";
            var station = Owner.FindItem(id)?.TryCast<MixingStation>();
            var stepIndex = Enumerable.Range(0, Math.Min(4, _plan.Steps.Count)).FirstOrDefault(i => _plan.ResolveMixerId(i) == id && id != "", -1);
            var expectedIngredient = stepIndex < 0 ? "" : _plan.Steps[stepIndex].IngredientId;
            var expectedSource = stepIndex < 0 ? "" : _plan.ResolveSupplyId(stepIndex);
            var route = routes.FirstOrDefault(r => r.Destination == id && r.Source == expectedSource && id != "")
                ?? routes.FirstOrDefault(r => r.Destination == id && r.Source != _plan.InputShelfId && id != "");
            var slotFilter = station?.MixerSlot.PlayerFilter;
            var filterCorrect = slotFilter != null && slotFilter.Type == SlotFilter.EType.Whitelist && slotFilter.ItemIDs.Count == 1 && slotFilter.ItemIDs[0] == expectedIngredient;
            var definition = Registry.GetItem(expectedIngredient);
            var routeAllowsIngredient = route != null && definition != null && (route.Route.Filter.Mode == ManagementItemFilter.EMode.Whitelist ? route.Route.Filter.Contains(definition) : !route.Route.Filter.Contains(definition));
            var actualLoading = route != null && route.Source == expectedSource && filterCorrect && routeAllowsIngredient;
            var pendingLoading = handlerReady && stepIndex >= 0 && route == null;
            var loadingCorrect = actualLoading || pendingLoading;
            var sourceCard = Panel("Actual ingredient supply", view, x, .17f, .23f, .14f, new Color(.16f, .24f, .31f));
            Label(sourceCard.transform, stepIndex < 0 ? "Unused" : EquipmentName(route?.Source ?? expectedSource, "No supply"), .04f, .04f, .92f, .92f, .016f, stepIndex < 0 ? null : loadingCorrect ? LinkGood : LinkBad, alignment: TextAlignmentOptions.Center);
            if (stepIndex >= 0)
            {
                FlowArrow(view, x + .115f, .315f, x + .115f, .425f, loadingCorrect ? LinkGood : LinkBad);
                WorkerBadge(view, route?.Worker ?? (pendingLoading ? handler : null), x + .145f, .335f, HandlerColor);
            }
            var card = Panel("Actual mixer", view, x, .43f, .23f, .24f, new Color(.16f, .24f, .31f));
            Label(card.transform, station == null ? "Missing mixer" : EquipmentName(id), .055f, .02f, .68f, .31f, .018f, station == null ? LinkBad : null, style: FontStyles.Bold, alignment: TextAlignmentOptions.Center);
            var actualIngredients = new List<string>();
            if (slotFilter != null) for (var i = 0; i < slotFilter.ItemIDs.Count; i++) actualIngredients.Add(ItemName(slotFilter.ItemIDs[i]));
            Label(card.transform, actualIngredients.Count == 0 ? "No ingredient filter" : string.Join(", ", actualIngredients), .035f, .36f, .93f, .30f, .015f, stepIndex < 0 || filterCorrect ? null : LinkBad, alignment: TextAlignmentOptions.Center);
            var threshold = station?.stationConfiguration.StartThrehold.Value ?? 0;
            Label(card.transform, "Threshold " + threshold, .035f, .70f, .93f, .24f, .014f, threshold == 20 ? LinkGood : LinkBad, alignment: TextAlignmentOptions.Center);
            var actualChemist = station?.stationConfiguration.AssignedChemist.SelectedNPC?.TryCast<Employee>();
            var actualStation = actualChemist != null && StationListed(actualChemist, station!);
            WorkerBadge(card.rectTransform, actualChemist ?? (chemistReady ? chemist : null), .80f, .065f,
                actualStation || chemistReady && actualChemist == null ? ChemistColor : LinkBad, .15f, true);
            if (stepIndex < 0) continue;
            var destination = station?.stationConfiguration.Destination.SelectedObject;
            var expectedDestination = stepIndex + 1 < _plan.Steps.Count ? _plan.ResolveMixerId(stepIndex + 1) : _plan.OutputShelfId;
            var correct = destination != null && destination.GUID.ToString() == expectedDestination;
            var next = ids.IndexOf(expectedDestination);
            if (next >= 0) FlowArrow(view, x + .232f, .55f, next * .255f - .003f, .55f, correct ? LinkGood : LinkBad);
            else
            {
                var color = correct ? LinkGood : LinkBad;
                FlowLine(view, x + .215f, .675f, x + .215f, .815f, color);
                FlowLine(view, x + .215f, .815f, .98f, .815f, color);
                FlowArrow(view, .98f, .815f, .98f, .835f, color);
            }
            if (!correct) Label(view, destination == null ? "No destination" : "To: " + EquipmentName(destination.GUID.ToString()), x, .975f, .23f, .025f, .012f, LinkBad);
        }
        var output = Panel("Output shelf", view, .765f, .84f, .23f, .13f, new Color(.16f, .24f, .31f));
        Label(output.transform, "OUTPUT", .04f, .03f, .92f, .30f, .014f, style: FontStyles.Bold, alignment: TextAlignmentOptions.Center);
        Label(output.transform, EquipmentName(_plan.OutputShelfId, "Not assigned"), .04f, .38f, .92f, .55f, .016f, alignment: TextAlignmentOptions.Center);
    }

    private void WorkerBadge(RectTransform parent, Employee? employee, float x, float y, Color color, float width = .05f, bool showEmpty = false)
    {
        if (employee == null && !showEmpty) return;
        var height = width * parent.rect.width / Mathf.Max(1, parent.rect.height);
        var badge = Panel("Worker badge", parent, x, y, width, height, color);
        Icon(badge.transform, employee?.MugshotSprite, .04f, .04f, .92f, .92f);
    }

    private void FlowArrow(RectTransform parent, float x1, float y1, float x2, float y2, Color color)
    {
        FlowLine(parent, x1, y1, x2, y2, color);
        var dx = (x2 - x1) * parent.rect.width; var dy = (y2 - y1) * parent.rect.height;
        var length = Mathf.Sqrt(dx * dx + dy * dy); if (length < .001f) return;
        var nx = dx / length; var ny = dy / length;
        FlowLine(parent, x2, y2, x2 - (nx * 3 + ny * 2) / parent.rect.width, y2 - (ny * 3 - nx * 2) / parent.rect.height, color);
        FlowLine(parent, x2, y2, x2 - (nx * 3 - ny * 2) / parent.rect.width, y2 - (ny * 3 + nx * 2) / parent.rect.height, color);
    }

    private void FlowLine(RectTransform parent, float x1, float y1, float x2, float y2, Color color)
    {
        var line = Rect("Native connection", parent);
        var dx = (x2 - x1) * parent.rect.width; var dy = -(y2 - y1) * parent.rect.height;
        line.anchorMin = line.anchorMax = new Vector2(x1, 1 - y1);
        line.pivot = new Vector2(0, .5f); line.anchoredPosition = Vector2.zero;
        line.sizeDelta = new Vector2(Mathf.Sqrt(dx * dx + dy * dy), 1);
        line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
        var image = line.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
    }
}
