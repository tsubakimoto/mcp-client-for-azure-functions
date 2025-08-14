using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace mcp_client_for_azure_functions;

public class Function1
{
    private static readonly SseClientTransport ClientTransport = new(
        new SseClientTransportOptions
        {
            Name = "Microsoft Learn MCP Server",
            Endpoint = new Uri("https://learn.microsoft.com/api/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp
        });

    private readonly ILogger<Function1> _logger;

    public Function1(ILogger<Function1> logger)
    {
        _logger = logger;
    }

    [Function("SearchDocByQuery")]
    public async Task<IActionResult> RunSearchDocByQueryAsync([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        var client = await McpClientFactory.CreateAsync(ClientTransport);
        var tools = await client.ListToolsAsync();
        var tool = tools.FirstOrDefault(t => t.Name == "microsoft_docs_search");
        if (tool is null)
        {
            return new BadRequestObjectResult("No tools found in MCP server.");
        }
        _logger.LogInformation($"{tool.Name} ({tool.Description})");

        var result = await client.CallToolAsync(
            tool.Name,
            new Dictionary<string, object?>
            {
                ["query"] = "Please tell me about Azure Functions."
            });

        foreach (var content in result.Content)
        {
            if (content is TextContentBlock tb)
            {
                var contents = JsonSerializer.Deserialize<ICollection<MSLearnContent>>(tb.Text);
                return new OkObjectResult(contents);
            }
        }

        return new NotFoundResult();
    }

    [Function("FetchDocByUrl")]
    public async Task<IActionResult> RunFetchDocAsync([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        string url = req.Query["url"].ToString();
        if (string.IsNullOrEmpty(url))
        {
            return new BadRequestObjectResult("Please provide a URL in the query string.");
        }

        var client = await McpClientFactory.CreateAsync(ClientTransport);
        var tools = await client.ListToolsAsync();
        var tool = tools.FirstOrDefault(t => t.Name == "microsoft_docs_fetch");
        if (tool is null)
        {
            return new BadRequestObjectResult("No tools found in MCP server.");
        }
        _logger.LogInformation($"{tool.Name} ({tool.Description})");

        var result = await client.CallToolAsync(
            tool.Name,
            new Dictionary<string, object?>
            {
                ["url"] = url
            });

        foreach (var content in result.Content)
        {
            if (content is TextContentBlock tb)
            {
                return new OkObjectResult(tb.Text);
            }
        }

        return new NotFoundResult();
    }
}

public class MSLearnContent
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("contentUrl")]
    public string? ContentUrl { get; set; }
}
