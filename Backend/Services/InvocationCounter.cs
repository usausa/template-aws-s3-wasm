namespace Backend.Services;

using System.Threading;

// Counts invocations served by the current execution environment.
//
// Registered as a singleton, so the value survives warm invocations and resets when Lambda
// starts a new environment. Surfacing it in the response makes that lifecycle visible, and it
// is the reason this is a service rather than a static: state that belongs to the environment
// is exactly what the container is for.
public sealed class InvocationCounter
{
    private int count;

    public int Next() => Interlocked.Increment(ref count);
}
