using System;

namespace IntelligentSalesAssistantAPI.Exceptions
{
    // Kastas vid otillåten åtkomst (t.ex. saknad eller ogiltig autentisering)
    public class UnauthorizedException : Exception
    {
        public UnauthorizedException() { }
        public UnauthorizedException(string message) : base(message) { }
        public UnauthorizedException(string message, Exception inner) : base(message, inner) { }
    }
}
