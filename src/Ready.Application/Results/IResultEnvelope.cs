namespace Ready.Application.Results;

public interface IResultEnvelope
{
    string ResultType { get; }
    string Version { get; }
    object Payload { get; }
}
