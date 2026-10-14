namespace CivicConnect.Web.Application;

public sealed class DuplicateRequestNumberException : Exception
{
    public DuplicateRequestNumberException()
        : base("That request number is already used.")
    {
    }
}
