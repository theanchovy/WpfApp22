using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WpfApp22;

public class ApiService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://localhost:5038/api/events";

    public ApiService() => _httpClient = new HttpClient();

    public async Task<List<Event>> GetUpcomingEventsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/upcoming");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<Event>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<Event>();
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

        var json = JsonSerializer.Serialize(eventItem);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PutAsync($"{BaseUrl}/{eventItem.Id}", content);
        response.EnsureSuccessStatusCode();
    }

    // Метод для удаления события
    public async Task DeleteEventAsync(int eventId)
    {
        var response = await _httpClient.DeleteAsync($"{BaseUrl}/{eventId}");
        response.EnsureSuccessStatusCode();
    }
}