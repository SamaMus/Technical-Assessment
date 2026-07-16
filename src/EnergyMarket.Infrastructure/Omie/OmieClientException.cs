namespace EnergyMarket.Infrastructure.Omie;

public sealed class OmieClientException : Exception
{
    public OmieClientException(string message, Exception? inner = null) : base(message, inner) { }
}
