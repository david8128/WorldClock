namespace WorldClock.Models;

/// <summary>
/// Controls how the window content responds to resizing.
/// </summary>
public enum ScaleMode
{
    /// <summary>
    /// Default. Every element scales proportionally so the full UI is always
    /// visible regardless of window size. The content shrinks/grows like a
    /// miniature of the design layout.
    /// </summary>
    ProportionScale = 0,

    /// <summary>
    /// Each element maintains its designed minimum size. Scrollbars appear
    /// when the window is smaller than the minimum content size.
    /// </summary>
    MinLimit = 1,
}
