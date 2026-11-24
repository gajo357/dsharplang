using System.Runtime.CompilerServices;

// Scoped access for the optional provider assembly; the core API stays internal.
[assembly: InternalsVisibleTo("DSharp.Runtime.Sqlite")]
