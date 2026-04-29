using Xunit;

namespace WorldClock.Tests.UI;

/// <summary>
/// Marks all UI automation tests as sequential (no parallelism).
/// FlaUI launches a real process and must not run concurrently.
/// </summary>
[CollectionDefinition("UI Tests", DisableParallelization = true)]
public class UITestCollection { }
