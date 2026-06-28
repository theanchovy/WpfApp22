using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WpfApp22
{
public class ApiService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://localhost:5038/api/events";

    public ApiService() => _httpClient = new HttpClient();

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
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


        public async Task<List<Event>> SearchEventsAsync(string searchTerm, DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            try
            {
                var queryParams = new List<string>();
                if (!string.IsNullOrWhiteSpace(searchTerm))
                    queryParams.Add($"searchTerm={Uri.EscapeDataString(searchTerm)}");

                if (dateFrom.HasValue)
                {
                    // Преобразуем в UTC
                    var utcFrom = dateFrom.Value.ToUniversalTime();
                    queryParams.Add($"dateFrom={utcFrom:yyyy-MM-ddTHH:mm:ssZ}");
                }

                if (dateTo.HasValue)
                {
                    var utcTo = dateTo.Value.ToUniversalTime();
                    queryParams.Add($"dateTo={utcTo:yyyy-MM-ddTHH:mm:ssZ}");
                }

                var url = $"{BaseUrl}/search?" + string.Join("&", queryParams);
                System.Diagnostics.Debug.WriteLine($"Запрос: {url}");

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<Event>>(json, _serializerOptions)
                       ?? new List<Event>();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Ошибка поиска: {ex.Message}", ex);
            }
        }
    }
}