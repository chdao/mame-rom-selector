using System.Text.Json.Serialization;

namespace MameSelector.Models;

/// <summary>
/// Represents a ROM selection that can be exported/imported
/// </summary>
public class RomSelection
{
    /// <summary>
    /// Version of the selection file format
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// When this selection was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Name/description of this selection
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of this selection
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// List of ROM names in this selection
    /// </summary>
    public List<string> RomNames { get; set; } = new();

    /// <summary>
    /// Total number of ROMs in this selection
    /// </summary>
    [JsonIgnore]
    public int Count => RomNames.Count;
}
