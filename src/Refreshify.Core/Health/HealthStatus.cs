namespace Refreshify.Core.Health;

/// <summary>
/// How a check ended. The order matters: when one check covers several items, such as a card with a line per drive,
/// the card takes the worst result. Unknown outranks good, so a mixed card looks neutral rather than green.
/// </summary>
public enum HealthStatus
{
    Good = 0,
    Unknown = 1,
    Attention = 2,
    Problem = 3,
}
