#pragma warning disable IDE0130
namespace System.Runtime.CompilerServices;
#pragma warning restore IDE0130

#if !NET6_0_OR_GREATER
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
internal sealed class InterpolatedStringHandlerAttribute : Attribute
{
}
#endif
