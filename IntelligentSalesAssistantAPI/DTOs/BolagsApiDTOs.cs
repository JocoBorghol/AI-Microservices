using System.Text.Json.Serialization;
using System.Text.Json;

namespace IntelligentSalesAssistantAPI.DTOs
{
    // Svar-DTO för företagsinformation från BolagsAPI
    public record BolagsApiResponse
    {
        [JsonPropertyName("name")]
        public string? Namn { get; init; }

        [JsonPropertyName("company_name")]
        public string? CompanyNameSnake { get; init; }

        [JsonPropertyName("companyName")]
        public string? CompanyNameCamel { get; init; }

        [JsonPropertyName("namn")]
        public string? NamnSv { get; init; }

        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("industry")]
        public string? Industry { get; init; }

        [JsonPropertyName("industry_name")]
        public string? IndustryName { get; init; }

        [JsonPropertyName("business")]
        public JsonElement? Business { get; init; }

        [JsonPropertyName("sni")]
        public string? Sni { get; init; }

        [JsonPropertyName("address")]
        public AddressData? AdressInfo { get; init; }

        [JsonPropertyName("website")]
        public string? Website { get; init; }

        [JsonPropertyName("web")]
        public string? Web { get; init; }

        // Hjälp-properties för att förenkla åtkomst till vanliga fält
        public string? ResolvedName => Namn ?? CompanyNameSnake ?? CompanyNameCamel ?? NamnSv;
        public string? Adress => AdressInfo?.Street;
        public string? Ort => AdressInfo?.City;
        public string? Postnummer => AdressInfo?.PostalCode;
        public string? ResolvedWebsite => Website ?? Web;
        public string? ResolvedIndustry => Industry ?? IndustryName ?? ResolveBusinessName(Business) ?? Sni;

        // Hjälpmetod för att extrahera branschbeskrivning ur olika JSON-format
        private static string? ResolveBusinessName(JsonElement? business)
        {
            if (business is null) return null;

            var value = business.Value;

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            if (value.ValueKind == JsonValueKind.Object)
            {
                if (value.TryGetProperty("description", out var description) && description.ValueKind == JsonValueKind.String)
                    return description.GetString();

                if (value.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                    return name.GetString();

                if (value.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String)
                    return code.GetString();
            }

            return null;
        }
    }

    // DTO för adressdata kopplat till företag
    public record AddressData(
        [property: JsonPropertyName("street")] string? Street,
        [property: JsonPropertyName("postal_code")] string? PostalCode,
        [property: JsonPropertyName("city")] string? City
    );
}