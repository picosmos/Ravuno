namespace Ravuno.DataStorage.Attributes;

/// <summary>
/// Indicates that a DateTime property should be stored as local time in the database
/// instead of being converted to UTC. Use this attribute sparingly and only when
/// local time semantics are explicitly required.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class LocalTimeAttribute : Attribute
{
}