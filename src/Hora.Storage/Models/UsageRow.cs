namespace Hora.Storage.Models;

/// <summary>One usage aggregate row.</summary>
public sealed record UsageRow(string Date, string Exe, string Domain, long Seconds);
