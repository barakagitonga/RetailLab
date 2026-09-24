namespace RetailLab.Core;

public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException(string message)
        : base(message)
    {
    }
}
