namespace IntelligentSalesAssistantAPI.Services.WebsiteGenerator
{
    /// <summary>
    /// Service för mallhantering och rendering av hemsidor
    /// </summary>
    public interface ITemplateService
    {
        /// <summary>
        /// Läser HTML-mallen från disk
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>HTML-mall med placeholders</returns>
        /// <exception cref="Exceptions.TemplateException">Om mall-filen inte finns eller inte kan läsas</exception>
        Task<string> LoadTemplateAsync(CancellationToken ct = default);

        /// <summary>
        /// Renderar HTML-mall med AI-genererat innehåll
        /// </summary>
        /// <param name="template">HTML-mall med placeholders</param>
        /// <param name="content">AI-genererat innehåll</param>
        /// <param name="sanitizedCompanyName">Saniterat företagsnamn för filsökvägar</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Färdig HTML med ersatta placeholders</returns>
        /// <exception cref="Exceptions.TemplateException">Om rendering misslyckas</exception>
        Task<string> RenderTemplateAsync(
            string template, 
            IntelligentSalesAssistantAPI.DTOs.WebsiteContentResponse content,
            string sanitizedCompanyName,
            CancellationToken ct = default);

        /// <summary>
        /// Sparar genererad hemsida till disk
        /// </summary>
        /// <param name="companyName">Företagsnamn (används för mappnamn)</param>
        /// <param name="html">Färdig HTML</param>
        /// <param name="ct">Cancellation token</param>
        /// <exception cref="Exceptions.FileOperationException">Om filoperationen misslyckas</exception>
        Task SaveWebsiteAsync(string companyName, string html, CancellationToken ct = default);

        /// <summary>
        /// Raderar genererad hemsida från disk
        /// </summary>
        /// <param name="companyName">Företagsnamn (används för mappnamn)</param>
        /// <param name="ct">Cancellation token</param>
        /// <exception cref="Exceptions.FileOperationException">Om filoperationen misslyckas</exception>
        Task DeleteWebsiteAsync(string companyName, CancellationToken ct = default);

        /// <summary>
        /// Saniterar företagsnamn till giltigt mappnamn
        /// </summary>
        /// <param name="companyName">Företagsnamn</param>
        /// <returns>Saniterat mappnamn (lowercase, bindestreck, inga specialtecken)</returns>
        string SanitizeCompanyName(string companyName);
    }
}
