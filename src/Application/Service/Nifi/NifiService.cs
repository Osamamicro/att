using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Headers;
using Application.DTOs.Nifi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Service.Nifi;

public sealed class NifiService : INifiService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<NifiService> _logger;

    public NifiService(HttpClient httpClient, IConfiguration configuration, ILogger<NifiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = configuration["integration:NIFI:BaseUrl"] 
                   ?? throw new InvalidOperationException("NIFI BaseUrl is not configured");
    }

    public async Task<List<NifiAttendanceRecordDto>> GetAttendanceRecordsAsync(
        string personNumber, 
        DateTime startDate, 
        DateTime endDate)
    {
        try
        {
            var startDateStr = startDate.ToString("yyyy-MM-dd");
            var endDateStr = endDate.ToString("yyyy-MM-dd");
            
            var requestNonce = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var url = $"{_baseUrl}?calendar_start={startDateStr}&calendar_end={endDateStr}&emp={personNumber}&_ts={requestNonce}";
            
            _logger.LogInformation("Fetching attendance records from NIFI: {Url}", url);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true,
                MaxAge = TimeSpan.Zero,
                MustRevalidate = true
            };
            request.Headers.Pragma.ParseAdd("no-cache");

            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("NIFI API returned status code: {StatusCode}, Content: {Content}", 
                    response.StatusCode, errorContent);
                return new List<NifiAttendanceRecordDto>();
            }

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("NIFI API response content (first 500 chars): {Content}", 
                content.Length > 500 ? content.Substring(0, 500) : content);
            
            var records = System.Text.Json.JsonSerializer.Deserialize<List<NifiAttendanceRecordDto>>(
                content, 
                new System.Text.Json.JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true,
                    Converters = { new FlexibleStringJsonConverter() }
                });
            
            _logger.LogInformation("Successfully parsed {Count} records from NIFI API", records?.Count ?? 0);
            
            return records ?? new List<NifiAttendanceRecordDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching attendance records from NIFI for person {PersonNumber}", personNumber);
            return new List<NifiAttendanceRecordDto>();
        }
    }

    public async Task<List<NifiAttendanceRecordDto>> GetSectionAttendanceRecordsAsync(
        string section,
        DateTime startDate,
        DateTime endDate,
        string personNumber = "")
    {
        try
        {
            var startDateStr = startDate.ToString("yyyy-MM-dd");
            var endDateStr = endDate.ToString("yyyy-MM-dd");

            var requestNonce = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var encodedSection = Uri.EscapeDataString(section ?? string.Empty);
            var url = $"{_baseUrl}?calendar_start={startDateStr}&calendar_end={endDateStr}&sec={encodedSection}";
            if (!string.IsNullOrWhiteSpace(personNumber))
            {
                url += $"&emp={Uri.EscapeDataString(personNumber)}";
            }
            url += $"&_ts={requestNonce}";

            _logger.LogInformation("Fetching section attendance records from NIFI: {Url}", url);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true,
                MaxAge = TimeSpan.Zero,
                MustRevalidate = true
            };
            request.Headers.Pragma.ParseAdd("no-cache");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("NIFI section API returned status code: {StatusCode}, Content: {Content}",
                    response.StatusCode, errorContent);
                return new List<NifiAttendanceRecordDto>();
            }

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("NIFI section API response content (first 500 chars): {Content}",
                content.Length > 500 ? content.Substring(0, 500) : content);

            var records = System.Text.Json.JsonSerializer.Deserialize<List<NifiAttendanceRecordDto>>(
                content,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new FlexibleStringJsonConverter() }
                });

            _logger.LogInformation("Successfully parsed {Count} section records from NIFI API", records?.Count ?? 0);

            return records ?? new List<NifiAttendanceRecordDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching section attendance records from NIFI for section {Section}", section);
            return new List<NifiAttendanceRecordDto>();
        }
    }

    private sealed class FlexibleStringJsonConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString() ?? string.Empty,
                JsonTokenType.Number => ReadNumberAsString(ref reader),
                JsonTokenType.True => "true",
                JsonTokenType.False => "false",
                JsonTokenType.Null => string.Empty,
                _ => string.Empty
            };
        }

        private static string ReadNumberAsString(ref Utf8JsonReader reader)
        {
            if (reader.TryGetInt64(out var intValue))
            {
                return intValue.ToString(CultureInfo.InvariantCulture);
            }

            if (reader.TryGetDecimal(out var decimalValue))
            {
                return decimalValue.ToString(CultureInfo.InvariantCulture);
            }

            if (reader.TryGetDouble(out var doubleValue))
            {
                return doubleValue.ToString(CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
