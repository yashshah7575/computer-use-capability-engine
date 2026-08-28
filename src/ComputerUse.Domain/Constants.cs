namespace ComputerUse.Domain;

/// <summary>
/// Canonical string and numeric literals for artifacts, replay, CLI, and the local demo surface.
/// JSON on disk uses these same values.
/// </summary>
public static class Constants
{
    public static class Approval
    {
        public const string Draft = "draft";
        public const string Approved = "approved";
    }

    public static class Risk
    {
        public const string ReadOnly = "READ_ONLY";
        public const string Reversible = "REVERSIBLE";
        public const string Risky = "RISKY";
        public const string Irreversible = "IRREVERSIBLE";
    }

    public static class Action
    {
        public const string Navigate = "navigate";
        public const string Click = "click";
        public const string Type = "type";
        public const string Extract = "extract";
        public const string Checkpoint = "checkpoint";
        public const string Wait = "wait";
        public const string Finish = "finish";

        public static readonly string[] ReplayAllowlist =
        [
            Navigate, Click, Type, Extract, Checkpoint, Wait
        ];
    }

    public static class Recovery
    {
        public const string Dismiss = "dismiss";
        public const string Wait = "wait";
    }

    public static class Locator
    {
        public const string Css = "css";
        public const string Role = "role";
        public const string Text = "text";
        public const string Placeholder = "placeholder";
        public const string Label = "label";
        public const string Default = "default";
    }

    public static class Outcome
    {
        public const string MemberNotFound = "MEMBER_NOT_FOUND";
        public const string TransientInterruption = "TRANSIENT_INTERRUPTION";
        public const string MemberNotFoundText = "Record not found";
        public const string InterruptionText = "Service interruption";
        public const string ControlResolved = "control resolved";
    }

    public static class DegradationKind
    {
        public const string TierDegraded = "tier_degraded";
    }

    public static class Schema
    {
        public const string Version = "1.0.0";
    }

    public static class ArtifactId
    {
        public const string LookupSavingsBalance = "lookup-savings-balance";
        public const string OpenSubAccount = "open-sub-account";
        public const string LookupArtifactFile = LookupSavingsBalance + ".json";
        public const string SubAccountArtifactFile = OpenSubAccount + ".json";
    }

    public static class Field
    {
        public const string MemberId = "memberId";
        public const string BaseUrl = "baseUrl";
        public const string Balance = "balance";
        public const string StringType = "string";
        public const string DecimalType = "decimal";
    }

    public static class Template
    {
        public const string MemberId = "{{memberId}}";
        public const string BaseUrlRoot = "{{baseUrl}}/";
    }

    public static class StepId
    {
        public const string OpenHome = "open-home";
        public const string TypeId = "type-id";
        public const string Submit = "submit";
        public const string OpenMember = "open-member";
        public const string CheckpointMember = "checkpoint-member";
        public const string ExtractBalance = "extract-balance";
        public const string OpenSub = "open-sub";
        public const string Confirm = "confirm";
    }

    public static class Selector
    {
        public const string MemberNumberInput = "input[name=memberno]";
        public const string TableLink = "table a";
        public const string SavingsCell = "h2 + table tr:nth-child(2) td:nth-child(2)";
        public const string MemberHref = "a[href*='member']";
        public const string Body = "body";
        public const string DeadLocator = "#does-not-exist";
        public const string GenericLink = "a";
    }

    public static class Ui
    {
        public const string Lookup = "Lookup";
        public const string Dismiss = "Dismiss";
        public const string MemberRecord = "Member record";
        public const string OpenSubAccount = "Open sub-account";
        public const string ConfirmOpenSubAccount = "Confirm open sub-account";
        public const string ButtonRole = "button";
    }

    public static class Llm
    {
        public const string Tool = "tool";
        public const string Css = "css";
        public const string Text = "text";
        public const string Value = "value";
        public const string Parameter = "parameter";
        public const string ExtractName = "extractName";
        public const string OutputType = "outputType";
        public const string TextContains = "textContains";
        public const string Risk = "risk";
        public const string Role = "role";
        public const string Name = "name";
        public const string Placeholder = "placeholder";
        public const string Label = "label";
        public const string Url = "url";
    }

    public static class DiscoveryEvent
    {
        public const string RunStarted = "run_started";
        public const string Observation = "observation";
        public const string ModelDecision = "model_decision";
        public const string ActionStarted = "action_started";
        public const string ActionSucceeded = "action_succeeded";
        public const string ActionFailed = "action_failed";
        public const string Checkpoint = "checkpoint";
        public const string Extract = "extract";
        public const string ArtifactEmitted = "artifact_emitted";
        public const string RunCompleted = "run_completed";
    }

    public static class Member
    {
        public const string Known = "12345";
        public const string Unknown = "00000";
        public const string Transient = "88888";
        public const string Alternate = "22222";
    }

    public static class Network
    {
        public const string Loopback = "127.0.0.1";
        public const string Localhost = "localhost";
        public const int DemoBankPort = 5100;
        public const int OperatorPort = 5200;
        public const int TestDemoBankPort = 18510;
        public const int TestOperatorPort = 18765;
        public const int TestOperatorPortAlt = 18766;
        public const int TestOperatorPortResume = 18767;
        public const int TestOperatorPortCompleted = 18768;
        public const int CaptureDemoOperatorPort = 5201;

        public static string LoopbackUrl(int port) => $"http://{Loopback}:{port}";

        public static string DemoBankUrl => LoopbackUrl(DemoBankPort);

        public static string TestDemoBankUrl => LoopbackUrl(TestDemoBankPort);
    }

    public static class Timing
    {
        public const int WaitStepMilliseconds = 400;
        public const int LocatorTimeoutMilliseconds = 5000;
        public const int ResolveProbeMilliseconds = 1500;
        public const int DefaultMaxDiscoverySteps = 16;
        public const int NoProgressLimit = 5;
        public const int MaxStepAttempts = 2;
        public const int DefaultStabilityRuns = 5;
        public const int ObservedSnippetLength = 400;
    }

    public static class PathName
    {
        public const string Config = "config";
        public const string Allowlist = "allowlist.json";
        public const string Artifacts = "artifacts";
        public const string Evidence = "evidence";
        public const string Solution = "ComputerUse.sln";
        public const string FailureScreenshot = "failure.png";
        public const string Snapshot = "snapshot.txt";
        public const string Result = "result.json";
        public const string Discovery = "discovery";
        public const string DiscoveryLog = "discovery.jsonl";
        public const string ArtifactJson = "artifact.json";
    }

    public static class Flag
    {
        public const string Goal = "--goal";
        public const string Url = "--url";
        public const string Scripted = "--scripted";
        public const string Artifact = "--artifact";
        public const string MemberId = "--member-id";
        public const string SimulateFailure = "--simulate-failure";
        public const string AllowDraft = "--allow-draft";
        public const string Headed = "--headed";
        public const string Headless = "--headless";
        public const string Runs = "--runs";
    }

    public static class Cli
    {
        public const string Discover = "discover";
        public const string Replay = "replay";
        public const string Hitl = "hitl";
        public const string CaptureDemo = "capture-demo";
        public const string Approve = "approve";
        public const string Stability = "stability";
    }

    public static class Aws
    {
        public const string ModelIdEnv = "BEDROCK_MODEL_ID";
        public const string RegionEnv = "AWS_REGION";
        public const string DefaultModelId = "amazon.nova-lite-v1:0";
        public const string DefaultRegion = "us-east-1";
    }

    public static class Route
    {
        public const string Resume = "/resume";
        public const string Authorize = "/authorize";
        public const string Completed = "/completed";
        public const string Deny = "/deny";
        public const string Screenshot = "/screenshot";
        public const string LookupQueryUnknown = "/lookup?memberno=" + Member.Unknown;
        public const string SubAccountQueryKnown = "/subaccount?id=" + Member.Known;
    }
}
