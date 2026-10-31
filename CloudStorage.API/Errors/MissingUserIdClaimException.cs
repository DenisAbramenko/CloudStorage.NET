namespace CloudStorage.API.Errors;

public sealed class MissingUserIdClaimException : Exception
{
    public MissingUserIdClaimException() : base("Claim user ID is missing.")
    {
    }
}
