using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Net.Http.Json;

namespace Controllers
{
    [ApiController]
    [Route("api/wa")]
    public class MessageController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public MessageController(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        public record MessageRequest(string Number, string Text);

        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] MessageRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.Number) || string.IsNullOrWhiteSpace(req.Text))
                return BadRequest(new { error = "missing_number_or_text" });

            var apiKey = _config["AtentBot:ApiKey"];
            var baseUrl = _config["AtentBot:BaseUrl"] ?? "https://api.atentbot.com";
            if (string.IsNullOrWhiteSpace(apiKey))
                return StatusCode(500, new { error = "atentbot_apikey_not_configured" });

            // monta requisição
            var client = _httpClientFactory.CreateClient();
            var url = $"{baseUrl.TrimEnd('/')}/message/sendText/crescer";
            var payload = new { number = req.Number, text = req.Text };

            using var http = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload)
            };
            http.Headers.Add("apikey", apiKey);

            var resp = await client.SendAsync(http, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                return StatusCode((int)resp.StatusCode, new { error = "whatsapp_send_failed", provider = body });

            return Ok(new { status = "sent", provider = body });
        }
    }
}

