using System.Collections.Generic;

namespace AnyToneCPS.Models;

public sealed class CsvPreviewRow
{
    public IReadOnlyList<string> Cells { get; init; } = [];
}
