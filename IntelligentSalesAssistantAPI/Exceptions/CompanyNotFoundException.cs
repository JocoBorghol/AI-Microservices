namespace IntelligentSalesAssistantAPI.Exceptions
{
    /// <summary>
    /// Kastas när ett företag inte hittas i BolagsAPI
    /// </summary>
    public class CompanyNotFoundException : NotFoundException
    {
        public CompanyNotFoundException(string orgNumber)
            : base($"Företaget med organisationsnummer '{orgNumber}' hittades inte i BolagsAPI.") { }
    }
}
