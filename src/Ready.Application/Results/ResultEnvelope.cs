namespace Ready.Application.Results;

public sealed record ResultEnvelope<T>(string ResultType, string Version, T Payload) : IResultEnvelope
{
    object IResultEnvelope.Payload => Payload!;
}
