namespace RetailLab.Web.Models;

/// <summary>
/// One demo copy section rendered in a detail-page accordion. Always demo:
/// marked in code (DemoProductContent) and labeled "Demo" in the UI.
/// </summary>
public sealed class DemoAccordion
{
    public required string Title { get; init; }

    public required string Body { get; init; }
}

/// <summary>
/// One demo review line for the detail page. Always demo: seeded in code and
/// rendered under the demo-reviews caption, never presented as real feedback.
/// </summary>
public sealed class DemoReview
{
    public string Author { get; init; } = "Demo shopper";

    public int Rating { get; init; }

    public required string Text { get; init; }

    public string Time { get; init; } = "Demo timeline";

    public string SourceType { get; init; } = "Simulated review response";

    public bool IsDemo => true;
}

public sealed record DemoReviewSummary(double AverageRating, int Count)
{
    public static DemoReviewSummary Empty { get; } = new(0, 0);
}
