namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Fixed process counters observable without a collector or durable IO. Labels are a closed vocabulary.</summary>
public static class IndependentReceivingDiagnostics
{
    private static readonly long[] Counts=new long[Enum.GetValues<IndependentReceivingFailure>().Length];
    /// <summary>Reads the bounded counter for a fixed receiving failure class.</summary>
    public static long Count(IndependentReceivingFailure code)=>Interlocked.Read(ref Counts[(int)code]);
    internal static void Record(IndependentReceivingFailure code)=>Interlocked.Increment(ref Counts[(int)code]);
    internal static void ObserveStatus(int? status)
    {
        if(status==403) Record(IndependentReceivingFailure.AuthorityRefused);
        else if(status==400) Record(IndependentReceivingFailure.MalformedRequest);
        else if(status==503) Record(IndependentReceivingFailure.EnrolledStateUnavailable);
    }
}
