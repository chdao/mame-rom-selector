using System.Xml;
using System.Xml.Linq;
using MameSelector.Models;

namespace MameSelector.Services;

/// <summary>
/// Service for parsing MAME XML files and extracting game information
/// </summary>
public class MameXmlParser
{
    /// <summary>
    /// Parses a MAME XML file and returns a list of games
    /// </summary>
    /// <param name="xmlFilePath">Path to the MAME XML file</param>
    /// <param name="progress">Optional progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of MAME games</returns>
    public async Task<List<MameGame>> ParseAsync(string xmlFilePath, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var games = new List<MameGame>();
        
        if (!File.Exists(xmlFilePath))
            throw new FileNotFoundException($"MAME XML file not found: {xmlFilePath}");

        using var fileStream = new FileStream(xmlFilePath, FileMode.Open, FileAccess.Read);
        using var reader = XmlReader.Create(fileStream, new XmlReaderSettings
        {
            Async = true,
            IgnoreWhitespace = true,
            IgnoreComments = true,
            DtdProcessing = DtdProcessing.Parse,
            ValidationType = ValidationType.None
        });

        var totalGames = 0;
        var processedGames = 0;

        // First pass to count total games for progress reporting
        if (progress != null)
        {
            totalGames = await CountGamesAsync(xmlFilePath, cancellationToken);
        }

        while (await reader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            

            if (reader.NodeType == XmlNodeType.Element && reader.Name == "machine")
            {
                var nameAttr = reader.GetAttribute("name");
                
                
                var game = await ParseGameAsync(reader, cancellationToken);
                if (game != null)
                {
                    
                    games.Add(game);
                    processedGames++;
                    
                    
                    if (progress != null && totalGames > 0)
                    {
                        var percentage = (int)((double)processedGames / totalGames * 100);
                        progress.Report(percentage);
                    }
                }
            }
        }

        return games;
    }

    /// <summary>
    /// Counts the total number of games in the XML file for progress reporting
    /// </summary>
    private async Task<int> CountGamesAsync(string xmlFilePath, CancellationToken cancellationToken)
    {
        var count = 0;
        using var fileStream = new FileStream(xmlFilePath, FileMode.Open, FileAccess.Read);
        using var reader = XmlReader.Create(fileStream, new XmlReaderSettings
        {
            Async = true,
            IgnoreWhitespace = true,
            IgnoreComments = true,
            DtdProcessing = DtdProcessing.Parse,
            ValidationType = ValidationType.None
        });

        while (await reader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "machine")
                count++;
        }

        return count;
    }

    /// <summary>
    /// Parses a single game element from the XML using simple XElement approach
    /// </summary>
    private MameGame? ParseGameFromElement(XElement machineElement)
    {
        var name = machineElement.Attribute("name")?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(name))
            return null;

        // Skip BIOS and device entries
        var isBios = machineElement.Attribute("isbios")?.Value == "yes";
        var isDevice = machineElement.Attribute("isdevice")?.Value == "yes";
        
        
        if (isBios || isDevice)
            return null;

        var game = new MameGame
        {
            Name = name,
            CloneOf = machineElement.Attribute("cloneof")?.Value ?? string.Empty,
            Description = machineElement.Element("description")?.Value?.Trim() ?? string.Empty,
            Year = machineElement.Element("year")?.Value?.Trim() ?? string.Empty,
            Manufacturer = machineElement.Element("manufacturer")?.Value?.Trim() ?? string.Empty
        };


        // Parse ROM files
        foreach (var romElement in machineElement.Elements("rom"))
        {
            var romFile = ParseRomElementFromXElement(romElement);
            if (romFile != null)
                game.RomFiles.Add(romFile);
        }

        // Parse disk files
        foreach (var diskElement in machineElement.Elements("disk"))
        {
            var diskFile = ParseDiskElementFromXElement(diskElement);
            if (diskFile != null)
                game.DiskFiles.Add(diskFile);
        }

        return game;
    }

    /// <summary>
    /// Parses a single game element from the XML with improved error handling
    /// </summary>
    private Task<MameGame?> ParseGameAsync(XmlReader reader, CancellationToken cancellationToken)
    {
        var nameAttr = reader.GetAttribute("name");
        
        try
        {
            // Check for BIOS and device entries first
            var isBios = reader.GetAttribute("isbios") == "yes";
            var isDevice = reader.GetAttribute("isdevice") == "yes";
            
            if (isBios || isDevice)
                return Task.FromResult<MameGame?>(null);

            if (string.IsNullOrEmpty(nameAttr))
                return Task.FromResult<MameGame?>(null);

            // Use step-by-step parsing to avoid skipping elements
            return Task.FromResult(ParseMachineElementRobust(reader, nameAttr));
        }
        catch (Exception)
        {
            // Try to recover by reading to the end of the current element
            try
            {
                while (reader.Read() && reader.NodeType != XmlNodeType.EndElement)
                {
                    // Skip content until we find the end element
                }
            }
            catch
            {
                // If recovery fails, just continue
            }
            
            return Task.FromResult<MameGame?>(null);
        }
    }

    /// <summary>
    /// Parses a machine element robustly without using XElement.ReadFrom
    /// </summary>
    private MameGame? ParseMachineElementRobust(XmlReader reader, string name)
    {
        var game = new MameGame
        {
            Name = name,
            CloneOf = reader.GetAttribute("cloneof") ?? string.Empty
        };

        // Read through the machine element content step by step
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "machine")
            {
                break; // End of machine element
            }
            
            if (reader.NodeType == XmlNodeType.Element)
            {
                switch (reader.Name)
                {
                    case "description":
                        if (reader.Read() && reader.NodeType == XmlNodeType.Text)
                        {
                            game.Description = reader.Value.Trim();
                        }
                        break;
                    case "year":
                        if (reader.Read() && reader.NodeType == XmlNodeType.Text)
                        {
                            game.Year = reader.Value.Trim();
                        }
                        break;
                    case "manufacturer":
                        if (reader.Read() && reader.NodeType == XmlNodeType.Text)
                        {
                            game.Manufacturer = reader.Value.Trim();
                        }
                        break;
                    case "rom":
                        var romFile = ParseRomElementRobust(reader);
                        if (romFile != null)
                            game.RomFiles.Add(romFile);
                        break;
                    case "disk":
                        var diskFile = ParseDiskElementRobust(reader);
                        if (diskFile != null)
                            game.DiskFiles.Add(diskFile);
                        break;
                    default:
                        // Skip unknown elements
                        reader.Skip();
                        break;
                }
            }
        }

        return game;
    }

    /// <summary>
    /// Parses a ROM element robustly
    /// </summary>
    private RomFile? ParseRomElementRobust(XmlReader reader)
    {
        try
        {
            var name = reader.GetAttribute("name");
            var size = reader.GetAttribute("size");
            var crc = reader.GetAttribute("crc");
            var sha1 = reader.GetAttribute("sha1");

            if (string.IsNullOrEmpty(name))
                return null;

            var romFile = new RomFile { Name = name };

            if (long.TryParse(size, out var sizeValue))
                romFile.Size = sizeValue;

            if (!string.IsNullOrEmpty(crc))
                romFile.CRC = crc;

            if (!string.IsNullOrEmpty(sha1))
                romFile.SHA1 = sha1;

            return romFile;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses a disk element robustly
    /// </summary>
    private DiskFile? ParseDiskElementRobust(XmlReader reader)
    {
        try
        {
            var name = reader.GetAttribute("name");
            var sha1 = reader.GetAttribute("sha1");
            var status = reader.GetAttribute("status");

            if (string.IsNullOrEmpty(name))
                return null;

            var diskFile = new DiskFile { Name = name };

            if (!string.IsNullOrEmpty(sha1))
                diskFile.SHA1 = sha1;

            return diskFile;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Safely reads element content with proper error handling and debugging
    /// </summary>
    private async Task<string> ReadElementContentSafeAsync(XmlReader reader, string gameName, string elementType)
    {
        try
        {
            if (reader.IsEmptyElement)
            {
                if (gameName.StartsWith("1on1") || gameName.StartsWith("2mind"))
                {
                    System.Diagnostics.Debug.WriteLine($"DEBUG: Empty {elementType} element for {gameName}");
                }
                return string.Empty;
            }

            var content = await reader.ReadElementContentAsStringAsync();
            var trimmedContent = content?.Trim() ?? string.Empty;
            
            if (gameName.StartsWith("1on1") || gameName.StartsWith("2mind"))
            {
                System.Diagnostics.Debug.WriteLine($"DEBUG: Read {elementType} for {gameName}: '{trimmedContent}' (Raw: '{content}')");
            }
            
            return trimmedContent;
        }
        catch (Exception ex)
        {
            if (gameName.StartsWith("1on1") || gameName.StartsWith("2mind"))
            {
                System.Diagnostics.Debug.WriteLine($"DEBUG: Error reading {elementType} for {gameName}: {ex.Message}");
            }
            return string.Empty;
        }
    }

    /// <summary>
    /// Parses a ROM element from XElement
    /// </summary>
    private RomFile? ParseRomElementFromXElement(XElement romElement)
    {
        var name = romElement.Attribute("name")?.Value;
        if (string.IsNullOrEmpty(name))
            return null;

        return new RomFile
        {
            Name = name,
            Size = long.TryParse(romElement.Attribute("size")?.Value, out var size) ? size : 0,
            CRC = romElement.Attribute("crc")?.Value ?? string.Empty,
            SHA1 = romElement.Attribute("sha1")?.Value ?? string.Empty,
            MD5 = romElement.Attribute("md5")?.Value ?? string.Empty
        };
    }

    /// <summary>
    /// Parses a disk element from XElement
    /// </summary>
    private DiskFile? ParseDiskElementFromXElement(XElement diskElement)
    {
        var name = diskElement.Attribute("name")?.Value;
        if (string.IsNullOrEmpty(name))
            return null;

        return new DiskFile
        {
            Name = name,
            SHA1 = diskElement.Attribute("sha1")?.Value ?? string.Empty,
            MD5 = diskElement.Attribute("md5")?.Value ?? string.Empty
        };
    }

    /// <summary>
    /// Parses a ROM element (legacy XmlReader method)
    /// </summary>
    private RomFile? ParseRomElement(XmlReader reader)
    {
        var name = reader.GetAttribute("name");
        if (string.IsNullOrEmpty(name))
            return null;

        var sizeStr = reader.GetAttribute("size");
        long.TryParse(sizeStr, out var size);

        return new RomFile
        {
            Name = name,
            Size = size,
            CRC = reader.GetAttribute("crc") ?? string.Empty,
            SHA1 = reader.GetAttribute("sha1") ?? string.Empty,
            MD5 = reader.GetAttribute("md5") ?? string.Empty
        };
    }

    /// <summary>
    /// Parses a disk element (CHD files)
    /// </summary>
    private DiskFile? ParseDiskElement(XmlReader reader)
    {
        var name = reader.GetAttribute("name");
        if (string.IsNullOrEmpty(name))
            return null;

        return new DiskFile
        {
            Name = name,
            SHA1 = reader.GetAttribute("sha1") ?? string.Empty,
            MD5 = reader.GetAttribute("md5") ?? string.Empty
        };
    }

}

