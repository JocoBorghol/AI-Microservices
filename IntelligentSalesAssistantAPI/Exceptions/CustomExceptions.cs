using System;
using System.Collections.Generic;

namespace IntelligentSalesAssistantAPI.Exceptions
{
    // Bas för alla domänspecifika undantag
    public abstract class BaseException : Exception
    {
        protected BaseException(string message) : base(message) { }
        protected BaseException(string message, Exception? inner) : base(message, inner) { }
    }

    // Kastas när en resurs inte hittas
    public class NotFoundException : BaseException
    {
        public NotFoundException(string resource, string id)
            : base($"{resource} med id '{id}' kunde inte hittas.") { }
        public NotFoundException(string message) : base(message) { }
    }

    // Kastas vid valideringsfel
    public class ValidationException : BaseException
    {
        public IReadOnlyList<string> Errors { get; }

        public ValidationException(IEnumerable<string> errors)
            : base("En eller flera valideringsfel har inträffat.")
        {
            Errors = new List<string>(errors);
        }

        public ValidationException(string error)
            : this(new List<string> { error }) { }
    }

    // Kastas vid fel från extern tjänst
    public class ExternalServiceException : BaseException
    {
        public string ServiceName { get; }
        public ExternalServiceException(string serviceName, string message)
            : base($"Fel från extern tjänst '{serviceName}': {message}")
        {
            ServiceName = serviceName;
        }
    }
}
