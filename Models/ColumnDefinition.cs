using System;

namespace SLSKDONET.Models;

public class ColumnDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public double? Width { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public string? PropertyPath { get; set; }
}
