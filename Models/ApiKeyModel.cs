namespace Models
{
    public class ApiKey
    {
        public sealed class Options
        {
            public string HeaderName { get; set; } = "X-API-KEY";
            public bool RequireIdHeader { get; set; } = false;
            public string IdHeaderName { get; set; } = "X-API-KEY-ID";
            public List<Record> Keys { get; set; } = new();
        }

        public sealed class Record
        {
            public string Id { get; set; } = default!;
            public string HashBase64 { get; set; } = default!; // SHA-256 da chave
            public bool Active { get; set; } = true;
            public DateTimeOffset? NotBefore { get; set; }
            public DateTimeOffset? NotAfter { get; set; }
        }

    }
}
