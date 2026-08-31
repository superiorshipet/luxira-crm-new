namespace Luxira.Application.Abstractions.Persistence;

public sealed class ReadStoreUnavailableException(string message)
    : InvalidOperationException(message);
