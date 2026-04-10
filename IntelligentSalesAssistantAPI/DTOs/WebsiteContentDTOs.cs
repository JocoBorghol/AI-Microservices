using System.ComponentModel.DataAnnotations;

namespace IntelligentSalesAssistantAPI.DTOs
{
    /// <summary>
    /// Request till Service B för att generera hemsideinnehåll - inkluderar all SuperEnrich-data
    /// </summary>
    public record GenerateWebsiteContentRequest(
        [Required] string CompanyName,
        [Required] string Industry,
        [Required] string City,
        string? Ceo,
        int? Employees,
        string? Founded,
        // Kontaktuppgifter från BolagsAPI
        string? Phone,
        string? Email,
        string? Website,
        string? Address,
        // Anpassningar från användaren
        string? Tone,
        string? TargetAudience,
        List<string>? TopServices,
        List<string>? Keywords,
        [Required] string ClientId
    );

    /// <summary>
    /// Komplett hemsideinnehåll returnerat från Service B
    /// </summary>
    public record WebsiteContentResponse(
        string CompanyName,
        string Tagline,
        string LogoIcon,
        WebsiteHeroContent Hero,
        WebsiteTrustBand TrustBand,
        List<WebsiteValueCard> Values,
        WebsiteAboutContent About,
        List<WebsiteServiceCard> Services,
        List<WebsiteFaqCard> Faqs,
        WebsiteContactInfo Contact
    );

    public record WebsiteHeroContent(
        string Title,
        string Text,
        string BackgroundImageUrl,
        string CtaPrimary,
        string CtaSecondary
    );

    public record WebsiteTrustBand(
        string Trust1,
        string Trust2,
        string Trust3
    );

    public record WebsiteValueCard(
        string Icon,
        string Title,
        string Text
    );

    public record WebsiteAboutContent(
        string Subtitle,
        string Title,
        List<string> Paragraphs,
        string ImageUrl,
        string CtaText,
        string OwnerName,
        string OwnerTitle
    );

    public record WebsiteServiceCard(
        string Title,
        string Description,
        string ImageUrl
    );

    public record WebsiteFaqCard(
        string Icon,
        string Question,
        string Answer
    );

    public record WebsiteContactInfo(
        string IntroText,
        string Phone,
        string Email
    );
}
