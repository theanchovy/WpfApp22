using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WpfApp22;

public class ApiService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://localhost:5038/api/events";

    public ApiService() => _httpClient = new HttpClient();

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new DateTimeConverterUsingDateTimeParse() }
    };

    public async Task<List<Event>> GetUpcomingEventsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/upcoming");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<Event>>(json, _serializerOptions)
                   ?? new List<Event>();
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Ошибка подключения к API: {ex.Message}", ex);
        }
    }

    public class DateTimeConverterUsingDateTimeParse : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTime.Parse(reader.GetString()!);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("yyyy-MM-ddTHH:mm:ss"));
        }
    }

    public async Task AddEventAsync(Event newEvent)
    {
        var json = JsonSerializer.Serialize(newEvent);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(BaseUrl, content);
        response.EnsureSuccessStatusCode();
    }

    // Метод для редактирования события
    public async Task UpdateEventAsync(Event eventItem)
    {
        if (eventItem == null)
            throw new ArgumentNullException(nameof(eventItem));

        try
        {
            var json = JsonSerializer.Serialize(eventItem);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync($"{BaseUrl}/{eventItem.Id}", content);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Ошибка обновления события: {ex.Message}", ex);
        }
    }

    // Метод для удаления события
    public async Task DeleteEventAsync(int eventId)
    {
        var response = await _httpClient.DeleteAsync($"{BaseUrl}/{eventId}");
        response.EnsureSuccessStatusCode();
    }
}