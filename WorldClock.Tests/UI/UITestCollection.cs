using Xunit;

namespace WorldClock.Tests.UI;

/// <summary>
/// Marks all UI tests as sequential.
/// Avalonia.Headless windows must not share an AppBuilder across parallel threads.
/// </summary>
[CollectionDefinition("UI Tests", DisableParallelization = true)]
public class UITestCollection { }
