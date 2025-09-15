using MameSelector.Models;

namespace MameSelector.Services;

/// <summary>
/// Service for resolving parent-child ROM dependencies in merged ROMsets
/// </summary>
public class ParentRomResolver
{
    private readonly LoggingService? _loggingService;
    private Dictionary<string, List<string>>? _dependencyTree;
    private Dictionary<string, MameGame>? _gamesCache;

    public ParentRomResolver(LoggingService? loggingService = null)
    {
        _loggingService = loggingService;
    }

    /// <summary>
    /// Builds the parent-child dependency tree from MAME games
    /// </summary>
    /// <param name="games">Dictionary of MAME games</param>
    /// <returns>Dictionary mapping parent names to their clone names</returns>
    public Dictionary<string, List<string>> BuildDependencyTree(Dictionary<string, MameGame> games)
    {
        _loggingService?.LogInfo("Building parent-child dependency tree...");
        
        _gamesCache = games;
        _dependencyTree = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        // Build parent-child relationships
        foreach (var game in games.Values)
        {
            if (game.IsClone && !string.IsNullOrEmpty(game.CloneOf))
            {
                if (!_dependencyTree.ContainsKey(game.CloneOf))
                {
                    _dependencyTree[game.CloneOf] = new List<string>();
                }
                _dependencyTree[game.CloneOf].Add(game.Name);
            }
        }

        // Update MameGame objects with relationship information
        foreach (var kvp in _dependencyTree)
        {
            if (games.TryGetValue(kvp.Key, out var parentGame))
            {
                parentGame.ChildClones = kvp.Value;
                
                // Set parent reference for clones
                foreach (var cloneName in kvp.Value)
                {
                    if (games.TryGetValue(cloneName, out var cloneGame))
                    {
                        cloneGame.ParentGame = parentGame;
                    }
                }
            }
        }

        _loggingService?.LogInfo($"Built dependency tree: {_dependencyTree.Count} parents with {_dependencyTree.Values.Sum(clones => clones.Count)} clones");
        return _dependencyTree;
    }

    /// <summary>
    /// Gets all parent ROMs required for a clone
    /// </summary>
    /// <param name="cloneName">Name of the clone ROM</param>
    /// <param name="games">Dictionary of MAME games</param>
    /// <returns>List of parent ROM names required</returns>
    public List<string> GetRequiredParents(string cloneName, Dictionary<string, MameGame> games)
    {
        var requiredParents = new List<string>();
        
        if (!games.TryGetValue(cloneName, out var cloneGame) || !cloneGame.IsClone)
        {
            return requiredParents;
        }

        // Get direct parent
        if (!string.IsNullOrEmpty(cloneGame.CloneOf))
        {
            requiredParents.Add(cloneGame.CloneOf);
            
            // Recursively get grandparent, great-grandparent, etc.
            var grandParents = GetRequiredParents(cloneGame.CloneOf, games);
            requiredParents.AddRange(grandParents);
        }

        return requiredParents.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Validates that all required parent ROMs are available for a clone
    /// </summary>
    /// <param name="cloneName">Name of the clone ROM</param>
    /// <param name="scannedRoms">Dictionary of scanned ROMs</param>
    /// <param name="games">Dictionary of MAME games</param>
    /// <returns>Validation result</returns>
    public ParentValidationResult ValidateParentAvailability(
        string cloneName, 
        Dictionary<string, ScannedRom> scannedRoms,
        Dictionary<string, MameGame> games)
    {
        var result = new ParentValidationResult
        {
            CloneName = cloneName,
            IsValid = true
        };

        if (!games.TryGetValue(cloneName, out var cloneGame) || !cloneGame.IsClone)
        {
            result.IsValid = false;
            result.ErrorMessage = "Not a clone ROM";
            return result;
        }

        var requiredParents = GetRequiredParents(cloneName, games);
        
        foreach (var parentName in requiredParents)
        {
            if (!scannedRoms.ContainsKey(parentName))
            {
                result.MissingParents.Add(parentName);
                result.IsValid = false;
            }
            else
            {
                result.AvailableParents.Add(parentName);
            }
        }

        if (!result.IsValid)
        {
            result.ErrorMessage = $"Missing parent ROMs: {string.Join(", ", result.MissingParents)}";
        }

        return result;
    }

    /// <summary>
    /// Gets all clones that depend on a specific parent ROM
    /// </summary>
    /// <param name="parentName">Name of the parent ROM</param>
    /// <param name="games">Dictionary of MAME games</param>
    /// <returns>List of clone names that depend on this parent</returns>
    public List<string> GetDependentClones(string parentName, Dictionary<string, MameGame> games)
    {
        var dependentClones = new List<string>();

        if (_dependencyTree != null && _dependencyTree.TryGetValue(parentName, out var directClones))
        {
            dependentClones.AddRange(directClones);
            
            // Also get clones of clones (grandchildren)
            foreach (var directClone in directClones)
            {
                var grandChildren = GetDependentClones(directClone, games);
                dependentClones.AddRange(grandChildren);
            }
        }

        return dependentClones.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Analyzes ROM dependencies for a collection of ROMs
    /// </summary>
    /// <param name="scannedRoms">Dictionary of scanned ROMs</param>
    /// <param name="games">Dictionary of MAME games</param>
    /// <returns>Dependency analysis results</returns>
    public DependencyAnalysis AnalyzeDependencies(
        Dictionary<string, ScannedRom> scannedRoms,
        Dictionary<string, MameGame> games)
    {
        var analysis = new DependencyAnalysis();
        
        foreach (var rom in scannedRoms.Values)
        {
            if (rom.IsClone)
            {
                var validation = ValidateParentAvailability(rom.Name, scannedRoms, games);
                
                if (validation.IsValid)
                {
                    analysis.ValidClones.Add(rom.Name);
                }
                else
                {
                    analysis.InvalidClones.Add(rom.Name);
                    analysis.MissingDependencies.AddRange(validation.MissingParents);
                }
            }
            else if (rom.IsParentGame)
            {
                analysis.ParentGames.Add(rom.Name);
            }
        }

        analysis.MissingDependencies = analysis.MissingDependencies.Distinct().ToList();
        
        _loggingService?.LogInfo($"Dependency analysis: {analysis.ValidClones.Count} valid clones, {analysis.InvalidClones.Count} invalid clones, {analysis.MissingDependencies.Count} missing dependencies");
        
        return analysis;
    }
}

/// <summary>
/// Result of parent ROM validation
/// </summary>
public class ParentValidationResult
{
    public string CloneName { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<string> AvailableParents { get; set; } = new();
    public List<string> MissingParents { get; set; } = new();
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// Analysis of ROM dependencies across a collection
/// </summary>
public class DependencyAnalysis
{
    public List<string> ValidClones { get; set; } = new();
    public List<string> InvalidClones { get; set; } = new();
    public List<string> ParentGames { get; set; } = new();
    public List<string> MissingDependencies { get; set; } = new();
    
    public int TotalClones => ValidClones.Count + InvalidClones.Count;
    public int TotalParents => ParentGames.Count;
    public double ValidCloneRatio => TotalClones > 0 ? (double)ValidClones.Count / TotalClones : 0;
}
