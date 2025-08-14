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

        string query = req.Query["query"].ToString();
        if (string.IsNullOrEmpty(query))
        {
            return new BadRequestObjectResult("Please provide a query in the query string.");
        }

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
                ["query"] = query
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
    public async Task<IActionResult> RunFetchDocByUrlAsync([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
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

    [Function("FetchDocByQuery")]
    public async Task<IActionResult> RunFetchDocByQueryAsync([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        string query = req.Query["query"].ToString();
        if (string.IsNullOrEmpty(query))
        {
            return new BadRequestObjectResult("Please provide a query in the query string.");
        }

        var client = await McpClientFactory.CreateAsync(ClientTransport);
        var tools = await client.ListToolsAsync();

        var searchTool = tools.FirstOrDefault(t => t.Name == "microsoft_docs_search");
        var fetchTool = tools.FirstOrDefault(t => t.Name == "microsoft_docs_fetch");
        if (searchTool is null || fetchTool is null)
        {
            return new BadRequestObjectResult("No tools found in MCP server.");
        }
        _logger.LogInformation($"{searchTool.Name} ({searchTool.Description})");
        _logger.LogInformation($"{fetchTool.Name} ({fetchTool.Description})");

        var searchResult = await client.CallToolAsync(
            searchTool.Name,
            new Dictionary<string, object?>
            {
                ["query"] = query
            });

        List<MSLearnContent> contents = new();
        foreach (var content in searchResult.Content)
        {
            if (content is not TextContentBlock tb)
            {
                continue;
            }

            var c = JsonSerializer.Deserialize<ICollection<MSLearnContent>>(tb.Text);
            if (c is not null)
            {
                contents.AddRange(c);
            }
        }

        if (!contents.Any())
        {
            return new NotFoundResult();
        }

        IEnumerable<Uri> urls = contents.Where(c => !string.IsNullOrEmpty(c.ContentUrl))
            .Select(c => new Uri(c.ContentUrl!))
            .Distinct()
            .ToList();

        List<string> response = new();
        foreach (var url in urls)
        {
            var fetchResult = await client.CallToolAsync(
                fetchTool.Name,
                new Dictionary<string, object?>
                {
                    ["url"] = url
                });

            foreach (var c in fetchResult.Content)
            {
                if (c is TextContentBlock tb)
                {
                    response.Add(tb.Text);
                }
            }
        }

        return new OkObjectResult(response);
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
