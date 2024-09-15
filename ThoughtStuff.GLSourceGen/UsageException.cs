using System;

namespace ThoughtStuff.GLSourceGen;

/// <summary>
/// Exception thrown that should be converted to an error for the user (programmer).
/// </summary>
internal class UsageException(string message) : Exception(message)
{
}
