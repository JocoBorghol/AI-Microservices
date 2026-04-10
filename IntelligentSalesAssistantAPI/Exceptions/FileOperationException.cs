namespace IntelligentSalesAssistantAPI.Exceptions
{
    /// <summary>
    /// Kastas vid fel vid filsystemoperationer
    /// </summary>
    public class FileOperationException : Exception
    {
        public FileOperationException(string message) : base(message) { }
        public FileOperationException(string message, Exception inner) : base(message, inner) { }
    }
}
