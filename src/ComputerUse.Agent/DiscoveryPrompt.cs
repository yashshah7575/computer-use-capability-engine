namespace ComputerUse.Agent;

/// <summary>
/// Discovery LLM prompt. Describes the action schema only — no DemoBank selectors.
/// </summary>
public static class DiscoveryPrompt
{
    public static string Build(string goal, string observation, string? lastOutcome = null)
    {
        var last = string.IsNullOrWhiteSpace(lastOutcome)
            ? ""
            : "\nLast result: " + lastOutcome;
        return "You operate a bank back-office UI. Goal: " + goal + "\nObservation:\n" + observation + last +
            "\nReply with ONE JSON object only. No markdown. Tools: navigate, click, type, extract, checkpoint, finish." +
            " Progress: fill the member textbox from CONTROLS, click the submit/search button from CONTROLS," +
            " click the matching result row/link, checkpoint visible success text, extract savings as extractName=balance" +
            " with a stable locator (not the numeric value), then finish." +
            " If a CONTROLS textbox already has text equal to the member id in the goal, do not type again; click the submit/search button." +
            " After a successful type, the next tool must be click (not type)." +
            " Copy locators from CONTROLS: role plus name or text, label, placeholder." +
            " If nameAttr is set, you may set css to input[name=<nameAttr from that control>]." +
            " Prefer role/name, label, placeholder, or visible text; css is a last resort." +
            " parameter must be the name memberId (not the member number itself); put the typed digits in value if needed." +
            "\nSchema examples (placeholders only):" +
            " {\"tool\":\"type\",\"role\":\"textbox\",\"name\":\"<accessible name>\",\"parameter\":\"memberId\"}" +
            " {\"tool\":\"click\",\"role\":\"button\",\"name\":\"<accessible name>\"}" +
            " {\"tool\":\"checkpoint\",\"textContains\":\"<visible success text>\"}" +
            " {\"tool\":\"extract\",\"css\":\"<selector>\",\"extractName\":\"balance\",\"outputType\":\"decimal\"}" +
            " {\"tool\":\"finish\"}." +
            " Use parameter only for memberId or baseUrl. Do not invent other parameter names." +
            " Do not locate extracted outputs by their runtime value.";
    }
}
