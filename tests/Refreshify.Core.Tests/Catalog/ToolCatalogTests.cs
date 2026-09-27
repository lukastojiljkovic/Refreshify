using Refreshify.Core.Catalog;
using Refreshify.Core.Diagnostics;
using Refreshify.Core.Tools;

namespace Refreshify.Core.Tests.Catalog;

public class ToolCatalogTests
{
    private static IEnumerable<ToolInfo> Infos => ToolCatalog.All.Select(tool => tool.Info);

    private static IEnumerable<ToolInfo> Visible => Infos.Where(info => !info.Hidden);

    [Fact]
    public void Ids_are_unique_kebab_case() =>
        Assert.All(Infos.GroupBy(info => info.Id), group =>
        {
            Assert.Single(group);
            Assert.Matches("^[a-z]+(-[a-z]+)*$", group.Key);
        });

    [Fact]
    public void Every_tool_explains_itself() =>
        Assert.All(Infos, info =>
        {
            Assert.False(string.IsNullOrWhiteSpace(info.Name), info.Id);
            Assert.EndsWith(".", info.Description);
            Assert.False(string.IsNullOrWhiteSpace(info.Technical), info.Id);
        });

    [Fact]
    public void Visible_tools_have_an_icon() =>
        Assert.All(Visible, info => Assert.False(string.IsNullOrEmpty(info.Glyph), info.Id));

    [Fact]
    public void Troubleshooting_tools_say_when_to_use_them_and_are_never_in_run_all_by_default() =>
        Assert.All(Visible.Where(info => info.Category == ToolCategory.Troubleshooting), info =>
        {
            Assert.EndsWith(".", info.UseWhen);
            Assert.False(info.IncludedByDefault, info.Id);
        });

    [Fact]
    public void Hidden_tools_are_never_in_run_all() =>
        Assert.All(Infos.Where(info => info.Hidden), info => Assert.False(info.IncludedByDefault, info.Id));

    [Fact]
    public void Every_fix_a_known_issue_names_exists() =>
        Assert.All(KnownIssues.All.SelectMany(issue => issue.FixToolIds), id => Assert.NotNull(ToolCatalog.Find(id)));

    [Fact]
    public void Tools_are_listed_in_run_order()
    {
        var categories = Visible.Select(info => info.Category).ToList();
        Assert.Equal(categories.Order(), categories);

        var ids = Infos.Select(info => info.Id).ToList();
        Assert.True(ids.IndexOf("dism-restorehealth") < ids.IndexOf("sfc"));
        Assert.Equal("restore-point", ids[0]);
    }

    [Fact]
    public void Every_category_page_has_tools() =>
        Assert.All(Enum.GetValues<ToolCategory>().Where(category => category != ToolCategory.Safety),
            category => Assert.Contains(Visible, info => info.Category == category));

    [Fact]
    public void Find_is_case_sensitive_and_returns_null_for_unknown_ids()
    {
        Assert.Equal("sfc", ToolCatalog.Find("sfc")?.Info.Id);
        Assert.Null(ToolCatalog.Find("SFC"));
        Assert.Null(ToolCatalog.Find("format-c"));
    }
}
