using System.ComponentModel.DataAnnotations;

namespace ISA.ContentEngine.DTOs
{
    /// <summary>
    /// Request för att generera hemsideinnehåll - inkluderar all företagsdata
    /// </summary>
    public record GenerateWebsiteContentRequest(
        // <example>Kandy'z AB</example>
        [Required(ErrorMessage = "Företagsnamn är obligatoriskt")]
        string CompanyName,
        
        // <example>Detaljhandel med godis</example>
        [Required(ErrorMessage = "Bransch är obligatorisk")]
        string Industry,
        
        // <example>Stockholm</example>
        [Required(ErrorMessage = "Stad är obligatorisk")]
        string City,
        
        // <example>Anna Andersson</example>
        string? Ceo,
        
        // <example>5</example>
        int? Employees,
        
        // <example>2012</example>
        string? Founded,
        
        // Kontaktuppgifter från BolagsAPI
        // <example>08-123 45 67</example>
        string? Phone,
        
        // <example>info@kandyz.se</example>
        string? Email,
        
        // <example>https://kandyz.se</example>
        string? Website,
        
        // <example>Storgatan 12</example>
        string? Address,
        
        // Anpassningar från användaren
        // <example>professionell och välkomnande</example>
        string? Tone,
        
        // <example>familjer och barnfamiljer</example>
        string? TargetAudience,
        
        // <example>["Godis", "Choklad", "Presentkort"]</example>
        List<string>? TopServices,
        
        // <example>["kvalitet", "tradition", "glädje"]</example>
        List<string>? Keywords,
        
        // <example>Vi älskar att se glada barn!</example>
        string? OwnerQuote,
        
        // <example>ServiceA</example>
        [Required(ErrorMessage = "ClientId är obligatoriskt")]
        string ClientId
    );

    /// <summary>
    /// Komplett hemsideinnehåll genererat av AI
    /// </summary>
    public record WebsiteContentResponse(
        // <example>Kandy'z AB</example>
        string CompanyName,
        
        // <example>Din lokala godisbutik med hjärtat på rätt ställe</example>
        string Tagline,
        
        // <example>fas fa-candy-cane</example>
        string LogoIcon,
        
        HeroContent Hero,
        TrustBand TrustBand,
        List<ValueCard> Values,
        AboutContent About,
        List<ServiceCard> Services,
        List<FaqCard> Faqs,
        ContactInfo Contact
    );

    /// <summary>
    /// Hero-sektion innehåll
    /// </summary>
    public record HeroContent(
        // <example>Välkommen till Kandy'z</example>
        string Title,
        
        // <example>Där varje besök är en upplevelse</example>
        string Text,
        
        // <example></example>
        string BackgroundImageUrl,
        
        // <example>Besök oss idag</example>
        string CtaPrimary,
        
        // <example>Se vårt sortiment</example>
        string CtaSecondary
    );

    /// <summary>
    /// Trust band - förtroendeskapande element
    /// </summary>
    public record TrustBand(
        // <example>Över 200 sorter</example>
        string Trust1,
        
        // <example>Lokalt ägd sedan 2012</example>
        string Trust2,
        
        // <example>Fri frakt över 500 kr</example>
        string Trust3
    );

    /// <summary>
    /// Värderingskort
    /// </summary>
    public record ValueCard(
        // <example>fas fa-heart</example>
        string Icon,
        
        // <example>Kvalitet</example>
        string Title,
        
        // <example>Vi levererar högsta kvalitet i varje påse</example>
        string Text
    );

    /// <summary>
    /// Om oss-sektion innehåll
    /// </summary>
    public record AboutContent(
        // <example>Om oss</example>
        string Subtitle,
        
        // <example>Välkommen till Kandy'z</example>
        string Title,
        
        // <example>["Sedan 2012 har vi...", "Vi älskar godis!"]</example>
        List<string> Paragraphs,
        
        // <example></example>
        string ImageUrl,
        
        // <example>Kontakta oss</example>
        string CtaText,
        
        // <example>Anna Andersson</example>
        string OwnerName,
        
        // <example>Grundare &amp; VD</example>
        string OwnerTitle
    );

    /// <summary>
    /// Tjänstekort
    /// </summary>
    public record ServiceCard(
        // <example>Lösgodis</example>
        string Title,
        
        // <example>Över 200 sorter att välja mellan</example>
        string Description,
        
        // <example></example>
        string ImageUrl
    );

    /// <summary>
    /// FAQ-kort
    /// </summary>
    public record FaqCard(
        // <example>fas fa-question</example>
        string Icon,
        
        // <example>Hur kontaktar jag er?</example>
        string Question,
        
        // <example>Du kan ringa oss eller skicka ett meddelande via kontaktformuläret.</example>
        string Answer
    );

    /// <summary>
    /// Kontaktinformation
    /// </summary>
    public record ContactInfo(
        // <example>Kontakta oss för mer information</example>
        string IntroText,
        
        // <example>08-123 45 67</example>
        string Phone,
        
        // <example>info@kandyz.se</example>
        string Email
    );
}
