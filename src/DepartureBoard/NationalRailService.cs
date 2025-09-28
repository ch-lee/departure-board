using System.Text.Json.Serialization;
using NationalRail.OpenLdb;

public class TrainDepartureResult
{
    [JsonIgnore]
    public DateTime GeneratedAt { get; set; }
    
    [JsonPropertyName("generated_at")]
    public string FriendlyGeneratedAt => GeneratedAt.ToString("yyyy MMMM dd @ HH:mm");
    public List<TrainDeparture> Departures { get; set; }
}

public class StationInfo
{
    public string Name { get; set; }
    public string ScheduledTime { get; set; }
    public string EstimatedTime { get; set; }
}

public class TrainDeparture
{
    [JsonPropertyName("destination")] public string Destination { get; set; }

    [JsonPropertyName("destination_code")] public string DestinationCode { get; set; }

    [JsonPropertyName("scheduled_time")] public string ScheduledTime { get; set; }

    [JsonPropertyName("platform")] public string Platform { get; set; }

    [JsonPropertyName("expected")] public string ExpectedTime { get; set; }

    [JsonPropertyName("operator")] public string Operator { get; set; }

    [JsonPropertyName("cancel_reason")] public string CancelReason { get; set; }
    public bool IsCancelled { get; set; }

    [JsonPropertyName("carriage_length")] public int CarriageLength { get; set; }
    
    [JsonIgnore]
    public IList<StationInfo> Stops { get; set; }

    [JsonPropertyName("stops_count")] public int StopsCount => Stops.Count;

    [JsonPropertyName("last_estimated_time")]
    public string Last => Stops.Last().ScheduledTime;
}

public interface ITrainDepartureService
{
    Task<IEnumerable<TrainDeparture>> GetDeparturesAsync(string stationCode, int numRows);
}

public class NationalRailService 
{
    private readonly LDBServiceSoapClient _client;
    private readonly string _accessToken;

    public NationalRailService(string accessToken)
    {
        _accessToken = accessToken;
        _client = new LDBServiceSoapClient(LDBServiceSoapClient.EndpointConfiguration.LDBServiceSoap);
    }

    public async Task<TrainDepartureResult> GetDeparturesAsync(string fromStationCode, string toStationCode, int numRows = 4)
    {
        var accessToken = new AccessToken { TokenValue = _accessToken };

        var response =
            await _client.GetDepBoardWithDetailsAsync(accessToken, 10, fromStationCode, toStationCode, FilterType.to, 0,
                120);

        var generatedAt = response.GetStationBoardResult.generatedAt;

        var departures = response.GetStationBoardResult.trainServices
            ?.Select(x => new TrainDeparture
            {
                Destination = x.destination.First().locationName,
                DestinationCode = x.destination.First().crs,
                ExpectedTime =
                    x.etd.Contains(":")
                        ? $"expected @ {x.etd}"
                        : x.etd, // if it's a time, then it's the expected time, otherwise it will be a string for 'on time' or 'cancelled' 
                ScheduledTime = x.std,
                Operator = x.@operator,
                Platform = x.platform,
                CancelReason = x.cancelReason,
                IsCancelled = x.isCancelled,
                CarriageLength = x.length,
                Stops = x.subsequentCallingPoints
                    .First().callingPoint.Select(y => new StationInfo
                    {
                        Name = y.locationName,
                        ScheduledTime = y.st,
                        EstimatedTime = y.et
                    }).ToList()
            }).ToList() ?? new List<TrainDeparture>();

        return new TrainDepartureResult
        {
            GeneratedAt = generatedAt,
            // N.B Take first 5 departures, otherwise TRML will log an error such as: Large payload received (7690 bytes). should be less than 2kb.
            Departures = departures.Take(numRows).ToList() 
        };
    }
}