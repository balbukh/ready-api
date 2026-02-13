using Ready.Application.Results;

namespace Ready.Application.Steps;

public sealed class ResultEnvelope : IResultEnvelope
{
    public string ResultType { get; }
    public string Version { get; }
    public object Payload { get; }

    public ResultEnvelope(string resultType, string version, object payload)
    {
        ResultType = resultType;
        Version = version;
        Payload = payload;
    }
}
