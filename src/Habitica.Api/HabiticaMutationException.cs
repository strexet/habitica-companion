namespace Habitica.Api;

/// <summary>Transport or malformed acknowledgement for a non-idempotent foreground operation.</summary>
public sealed class HabiticaMutationException(bool requestDispatched,bool cancelled=false)
    : Exception(requestDispatched?"Habitica operation outcome is unknown. Do not replay the request.":"Habitica operation was not dispatched.")
{
    public bool RequestDispatched {get;}=requestDispatched;
    public bool Cancelled {get;}=cancelled;
}

public sealed class HabiticaMutationCanceledException(bool requestDispatched,CancellationToken token)
    : OperationCanceledException(requestDispatched?"Dispatched Habitica operation acknowledgement was cancelled; outcome is unknown.":"Habitica operation cancelled before dispatch.",token)
{
    public bool RequestDispatched {get;}=requestDispatched;
}
