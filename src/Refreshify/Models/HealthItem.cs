using Microsoft.UI.Xaml;
using Refreshify.Core.Catalog;
using Refreshify.Core.Health;

namespace Refreshify.Models;

/// <summary>A card on the Health page: one check, still running or finished.</summary>
public sealed class HealthItem(HealthCheck check) : Observable
{
    private HealthResult? _result;
    private bool _running = true;

    public HealthCheck Check { get; } = check;

    public string Name => Check.Name;

    public bool IsRunning => _running;

    public bool IsDone => !_running;

    public string Sentence => _result?.Sentence ?? string.Empty;

    public HealthAction? Action => _result?.Action;

    public bool HasAction => _result?.Action is not null;

    public string ActionLabel => _result?.Action is { } action ? $"Open {Destination(action)}" : string.Empty;

    public string IconGlyph => _running ? string.Empty : GlyphFor(_result?.Status);

    public Style? IconStyle => _running ? null : StyleFor(_result?.Status);

    public void Restart()
    {
        _running = true;
        _result = null;
        Changed(string.Empty);
    }

    public void Finish(HealthResult result)
    {
        _result = result;
        _running = false;
        Changed(string.Empty);
    }

    private static string Destination(HealthAction action) =>
        action.ToolId is { } id && ToolCatalog.Find(id) is { } tool ? tool.Info.Name : CategoryInfo.Get(action.Category).Name;

    private static string GlyphFor(HealthStatus? status) => status switch
    {
        HealthStatus.Good => Glyphs.HealthGood,
        HealthStatus.Attention => Glyphs.HealthCaution,
        HealthStatus.Problem => Glyphs.HealthProblem,
        _ => Glyphs.HealthUnknown,
    };

    private static Style StyleFor(HealthStatus? status) => Resource(status switch
    {
        HealthStatus.Good => "HealthGoodIconStyle",
        HealthStatus.Attention => "HealthCautionIconStyle",
        HealthStatus.Problem => "HealthCriticalIconStyle",
        _ => "HealthUnknownIconStyle",
    });

    private static Style Resource(string key) => (Style)Application.Current.Resources[key];

    public override string ToString() => Name;
}
