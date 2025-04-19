namespace System.Runtime.CompilerServices
{
#if !NET6_0_OR_GREATER
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    internal sealed class InterpolatedStringHandlerAttribute : Attribute
    {
    }
#endif

#if !NET5_0_OR_GREATER
    internal static class IsExternalInit;
#endif
}
