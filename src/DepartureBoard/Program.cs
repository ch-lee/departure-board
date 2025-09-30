// See https://aka.ms/new-console-template for more information

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DepartureBoard;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());
services.AddHttpClient();
services.AddLogging(builder =>
{
    builder.ClearProviders();
    builder.AddSimpleConsole(options =>
    {
        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
        options.UseUtcTimestamp = true;   // or false for local time
        options.SingleLine = true;     // optional: make each log a single line
    });

    builder.SetMinimumLevel(LogLevel.Information);
});
var provider = services.BuildServiceProvider();


var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Program");
var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
var httpClient = httpClientFactory.CreateClient();

var config = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

var routes = config.GetSection("Routes").Get<Route[]>() ?? [];

var networkRailAccessToken =  Environment.GetEnvironmentVariable("NETWORK_RAIL_ACCESS_TOKEN");

logger.LogInformation($"NETWORK_RAIL_ACCESS_TOKEN: {networkRailAccessToken.Substring(0,5)}******");

// env var for testing. Set this to true to avoid sending to TRMNL, because it has a 5 min rate limit.
// This is to test that at the very least it outputs to logs.
var skipTrmnlSendEnvVar = Environment.GetEnvironmentVariable("SKIP_SEND_TO_TRMNL");
logger.LogInformation($"SKIP_SEND_TRMNL: {skipTrmnlSendEnvVar}");

var skipTrmnlSend = true;

if (skipTrmnlSendEnvVar is not null)
{
    skipTrmnlSend = bool.Parse(skipTrmnlSendEnvVar);
    logger.LogInformation("SKIP_SEND_TO_TRMNL has been set to {skipTrmnlSend}", skipTrmnlSend);
}

var trmnlWebhookUrl = Environment.GetEnvironmentVariable("TRMNL_WEBHOOK_URL");
logger.LogInformation($"TRMNL_WEBHOOK_URL: {trmnlWebhookUrl}");
if (trmnlWebhookUrl is null)
{
    logger.LogError(
        "TRMNL_WEBHOOK_URL env var not set. Please get webhook url from: https://docs.usetrmnl.com/go/private-plugins/create-a-screen#authorization");
}

if (routes.Any() is false)
{
    logger.LogError("No routes added in env var");
}

if (string.IsNullOrEmpty(networkRailAccessToken))
{
    logger.LogError("NETWORK_RAIL_ACCESS_TOKEN env var not set");
}

if (string.IsNullOrEmpty(trmnlWebhookUrl) || string.IsNullOrEmpty(networkRailAccessToken) ||  routes.Any() is false)
{
    // if any required env not set, exit app. 
    return;
}

List<TrainDepartureResult> departureResults = [];

foreach (var route in routes)
{
    var departures = await GetDepartures(route.From, route.To);

    departureResults.Add(departures);
}

async Task<TrainDepartureResult> GetDepartures(string fromStationCode, string toStationCode)
{
    var nationalRailService = new NationalRailService(networkRailAccessToken);

    var results = await nationalRailService.GetDeparturesAsync(fromStationCode, toStationCode, 3);

    return results;
}

if (departureResults.Any())
{
    var trmnlWebhookRequest = new TrmnlWebhookRequest<TrainDepartureResult>
    {
        MergeVariables = new TrainDepartureResult
        {
            GeneratedAt = DateTime.Now,
            Departures = departureResults.SelectMany(x => x.Departures).OrderBy(x=>x.ScheduledTime).ToList()
        }
    };

    var serialize = JsonSerializer.Serialize(trmnlWebhookRequest);
    
    var utf8Bytes = System.Text.Encoding.UTF8.GetBytes(serialize);
    logger.LogInformation(serialize);
    logger.LogInformation("Payload size: {ByteCount} bytes", utf8Bytes.Length);
    

    if (utf8Bytes.Length > 2000)
    {
        logger.LogWarning(
            "Payload may be too large for TRMNL - check device logs. URL will be something like: https://usetrmnl.com/devices/{devideId}/logs");
    }

    if (skipTrmnlSend)
    {
        logger.LogInformation("Skipping sending to TRMNL webhook - avoiding the 5 min-rate limit");
    }
    else
    {
        var trmnlResponse = await httpClient.PostAsJsonAsync(trmnlWebhookUrl, trmnlWebhookRequest);
        logger.LogInformation("TRMNL status code: {StatusCode}", trmnlResponse.StatusCode);
    }
}