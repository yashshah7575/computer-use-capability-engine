namespace ComputerUse.Domain;

public sealed class InterventionRequest
{
    public string RunId { get; set; } = "";
    public string Reason { get; set; } = "";
    public string StepId { get; set; } = "";
    public string ScreenshotPath { get; set; } = "";
    public ControllerKind Controller { get; set; } = ControllerKind.Human;
}
