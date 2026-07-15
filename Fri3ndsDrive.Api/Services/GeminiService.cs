using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Fri3ndsDrive.Api.Services
{
    public class GeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public GeminiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<string> GenerarResumenAsync(string contenido)
        {
            try
            {
                string apiKey = _configuration["Gemini:ApiKey"] ?? "";
                string modelo = _configuration["Gemini:Model"] ?? "gemini-2.5-flash";

                if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Contains("AQUI"))
                {
                    return "Resumen IA no disponible: API Key de Gemini no configurada.";
                }

                string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelo}:generateContent";

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json")
                );

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new
                                {
                                    text = $"Resume el siguiente documento de forma breve, profesional y clara:\n\n{contenido}"
                                }
                            }
                        }
                    }
                };

                string json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                string responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return $"Resumen IA no disponible. Error {(int)response.StatusCode}: {responseText}";
                }

                using JsonDocument document = JsonDocument.Parse(responseText);

                return document.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString() ?? "No se pudo generar el resumen.";
            }
            catch (Exception ex)
            {
                return $"Resumen IA no disponible. Error interno: {ex.Message}";
            }
        }

        public async Task<string> GenerarEtiquetasAsync(string contenido)
        {
            try
            {
                string apiKey = _configuration["Gemini:ApiKey"] ?? "";
                string modelo = _configuration["Gemini:Model"] ?? "gemini-2.5-flash";

                if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Contains("AQUI"))
                {
                    return "sin-etiquetas";
                }

                string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelo}:generateContent";

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json")
                );

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new
                                {
                                    text = $"Genera únicamente entre 3 y 6 etiquetas separadas por comas para clasificar este documento. No expliques nada.\n\n{contenido}"
                                }
                            }
                        }
                    }
                };

                string json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                string responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return $"sin-etiquetas (Error {(int)response.StatusCode})";
                }

                using JsonDocument document = JsonDocument.Parse(responseText);

                return document.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString() ?? "sin-etiquetas";
            }
            catch
            {
                return "sin-etiquetas";
            }
        }
    }
}