using System.Text.RegularExpressions;
using ComputerUse.Domain;

namespace ComputerUse.Agent;

/// <summary>
/// Adds observation-derived locator fallbacks so the model does not have to guess CSS.
/// Does not inject DemoBank-specific selectors into the prompt.
/// </summary>
internal static class LocatorEnricher
{
    private static readonly Regex SafeName = new("^[A-Za-z][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant);

    public static List<LocatorSpec> For(ModelAction action, string observation)
    {
        var list = ModelActionParser.Locators(action);
        var controls = ObservationControlParser.Parse(observation);
        if (action.Tool == Constants.Action.Type)
            AppendTypeFallbacks(list, controls);
        else if (action.Tool == Constants.Action.Click)
        {
            AppendUniqueSubmit(list, controls);
            AppendUniqueMemberLink(list, controls);
        }
        else if (action.Tool == Constants.Action.Extract)
            AppendExtractFallbacks(list, controls, action.ExtractName);
        return list;
    }

    /// <summary>
    /// If the model types or extracts on a search-results page that has a unique member link,
    /// treat the action as a click on that link so discovery can proceed.
    /// </summary>
    public static ModelAction MaybeCoerce(ModelAction action, string observation)
    {
        var controls = ObservationControlParser.Parse(observation);
        if (HasSavingsCell(controls))
            return action;
        if (MemberLinks(controls).Count != 1)
            return action;
        if (action.Tool is Constants.Action.Type or Constants.Action.Extract or Constants.Action.Checkpoint)
            return new ModelAction { Tool = Constants.Action.Click };
        return action;
    }

    public static string? ProgressHint(string observation, ModelAction action)
    {
        var controls = ObservationControlParser.Parse(observation);
        if (controls.Count == 0)
            return null;
        if (HasSavingsCell(controls) && action.Tool == Constants.Action.Type)
            return "Member record is visible. Checkpoint, extract balance from the Savings cell, then finish. Do not type.";
        if (MemberLinks(controls).Count != 1)
            return null;
        if (action.Tool == Constants.Action.Type && !controls.Any(IsTextEntry))
            return "No textbox on this page. Click the result link from CONTROLS (role=link).";
        if (action.Tool == Constants.Action.Extract && !HasSavingsCell(controls))
            return "Savings is not visible yet. Click the member result link from CONTROLS (role=link).";
        return null;
    }

    public static bool MemberFieldAlreadyFilled(string observation, DiscoveryContext context)
    {
        if (!context.KnownInputs.TryGetValue(Constants.Field.MemberId, out var memberId) ||
            string.IsNullOrWhiteSpace(memberId))
            return false;
        return ObservationControlParser.Parse(observation).Any(c =>
            IsTextEntry(c) &&
            string.Equals(c.Text?.Trim(), memberId.Trim(), StringComparison.Ordinal));
    }

    private static void AppendTypeFallbacks(List<LocatorSpec> list, IReadOnlyList<ObservedControl> controls)
    {
        foreach (var c in controls)
        {
            if (!IsTextEntry(c))
                continue;
            if (!string.IsNullOrWhiteSpace(c.InputName) && SafeName.IsMatch(c.InputName))
            {
                var tag = string.IsNullOrWhiteSpace(c.Tag) ? "input" : c.Tag.Trim().ToLowerInvariant();
                Add(list, new LocatorSpec { Strategy = Constants.Locator.Css, Value = $"{tag}[name={c.InputName}]" });
            }

            if (!string.IsNullOrWhiteSpace(c.Label))
                Add(list, new LocatorSpec { Strategy = Constants.Locator.Label, Value = c.Label });
            if (!string.IsNullOrWhiteSpace(c.Placeholder))
                Add(list, new LocatorSpec { Strategy = Constants.Locator.Placeholder, Value = c.Placeholder });
            if (!string.IsNullOrWhiteSpace(c.Role))
            {
                var name = First(c.Name, c.Label, c.Text);
                if (!string.IsNullOrWhiteSpace(name))
                    Add(list, new LocatorSpec { Strategy = Constants.Locator.Role, Role = c.Role, Name = name });
            }
        }
    }

    private static void AppendUniqueSubmit(List<LocatorSpec> list, IReadOnlyList<ObservedControl> controls)
    {
        var submits = controls.Where(IsSubmit).ToList();
        if (submits.Count != 1)
            return;
        var c = submits[0];
        var name = First(c.Name, c.Text);
        if (!string.IsNullOrWhiteSpace(c.Role) && !string.IsNullOrWhiteSpace(name))
            Add(list, new LocatorSpec { Strategy = Constants.Locator.Role, Role = c.Role, Name = name });
        if (!string.IsNullOrWhiteSpace(c.Text))
            Add(list, new LocatorSpec { Strategy = Constants.Locator.Text, Value = c.Text });
    }

    private static void AppendUniqueMemberLink(List<LocatorSpec> list, IReadOnlyList<ObservedControl> controls)
    {
        var links = MemberLinks(controls);
        if (links.Count != 1)
            return;
        var c = links[0];
        Add(list, new LocatorSpec { Strategy = Constants.Locator.Css, Value = Constants.Selector.MemberHref });
        var name = First(c.Name, c.Text);
        if (!string.IsNullOrWhiteSpace(c.Role) && !string.IsNullOrWhiteSpace(name))
            Add(list, new LocatorSpec { Strategy = Constants.Locator.Role, Role = c.Role, Name = name });
        if (!string.IsNullOrWhiteSpace(c.Text))
            Add(list, new LocatorSpec { Strategy = Constants.Locator.Text, Value = c.Text });
    }

    private static List<ObservedControl> MemberLinks(IReadOnlyList<ObservedControl> controls) =>
        controls
            .Where(c =>
                (c.Href ?? "").Contains("member", StringComparison.OrdinalIgnoreCase) &&
                !(c.Href ?? "").Contains("subaccount", StringComparison.OrdinalIgnoreCase))
            .ToList();

    private static bool HasSavingsCell(IReadOnlyList<ObservedControl> controls) =>
        controls.Any(c =>
            string.Equals(c.Role, "cell", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(c.Label) &&
            IsExtractLabel(c.Label, Constants.Field.Balance));

    private static void AppendExtractFallbacks(
        List<LocatorSpec> list,
        IReadOnlyList<ObservedControl> controls,
        string? extractName)
    {
        foreach (var c in controls)
        {
            if (!string.Equals(c.Role, "cell", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.IsNullOrWhiteSpace(c.Label) || c.Label.Contains('"'))
                continue;
            if (!IsExtractLabel(c.Label, extractName))
                continue;
            Add(list, new LocatorSpec
            {
                Strategy = Constants.Locator.Css,
                Value = $"td:has-text(\"{c.Label.Trim()}\") + td"
            });
        }
    }

    private static bool IsExtractLabel(string label, string? extractName)
    {
        if (label.Contains("saving", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("balance", StringComparison.OrdinalIgnoreCase))
            return true;
        return !string.IsNullOrWhiteSpace(extractName) &&
               label.Contains(extractName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTextEntry(ObservedControl c)
    {
        var type = (c.Type ?? "").Trim().ToLowerInvariant();
        if (type is "submit" or "button" or "hidden" or "checkbox" or "radio")
            return false;
        var tag = (c.Tag ?? "").Trim().ToLowerInvariant();
        var role = (c.Role ?? "").Trim().ToLowerInvariant();
        return tag is "input" or "textarea" || role is "textbox" or "searchbox";
    }

    private static bool IsSubmit(ObservedControl c)
    {
        var type = (c.Type ?? "").Trim().ToLowerInvariant();
        if (type == "submit")
            return true;
        var tag = (c.Tag ?? "").Trim().ToLowerInvariant();
        var role = (c.Role ?? "").Trim().ToLowerInvariant();
        return tag == "button" || role == "button";
    }

    private static void Add(List<LocatorSpec> list, LocatorSpec spec)
    {
        if (list.Any(e =>
                e.Strategy == spec.Strategy &&
                e.Role == spec.Role &&
                e.Name == spec.Name &&
                e.Value == spec.Value))
            return;
        list.Add(spec);
    }

    private static string? First(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
