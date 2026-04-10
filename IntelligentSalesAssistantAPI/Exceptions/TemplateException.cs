namespace IntelligentSalesAssistantAPI.Exceptions
{
    /// <summary>
    /// Kastas vid fel relaterade till HTML-mallhantering
    /// </summary>
    public class TemplateException : Exception
    {
        public TemplateException(string message) : base(message) { }
        public TemplateException(string message, Exception inner) : base(message, inner) { }
    }
}
