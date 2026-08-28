using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using ComputerUse.Domain;

namespace ComputerUse.Agent;

public sealed class BedrockLanguageModel : ILanguageModel
{
    public async Task<string> CompleteAsync(string prompt)
    {
        var model = Environment.GetEnvironmentVariable(Constants.Aws.ModelIdEnv) ?? Constants.Aws.DefaultModelId;
        var region = Environment.GetEnvironmentVariable(Constants.Aws.RegionEnv) ?? Constants.Aws.DefaultRegion;
        using var client = new AmazonBedrockRuntimeClient(RegionEndpoint.GetBySystemName(region));
        var resp = await client.ConverseAsync(new ConverseRequest
        {
            ModelId = model,
            Messages =
            [
                new Message
                {
                    Role = ConversationRole.User,
                    Content = [new ContentBlock { Text = prompt }]
                }
            ]
        });
        return string.Join("", resp.Output.Message.Content.Select(c => c.Text)).Trim();
    }
}
