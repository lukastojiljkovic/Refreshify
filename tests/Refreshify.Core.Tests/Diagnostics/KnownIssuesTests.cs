using Refreshify.Core.Diagnostics;

namespace Refreshify.Core.Tests.Diagnostics;

public class KnownIssuesTests
{
    [Fact]
    public void Issues_have_unique_ids_and_plain_language_texts()
    {
        Assert.Equal(KnownIssues.All.Count, KnownIssues.All.Select(issue => issue.Id).Distinct().Count());
        Assert.All(KnownIssues.All, issue =>
        {
            Assert.False(string.IsNullOrWhiteSpace(issue.Title));
            Assert.EndsWith(".", issue.Explanation);
        });
    }

    [Fact]
    public void Remedies_that_run_a_fix_name_the_fix_tools_and_the_button()
    {
        Assert.All(KnownIssues.All.Where(issue => issue.Remedy is RemedyKind.Automatic or RemedyKind.AskFirst or RemedyKind.WindowsImage), issue =>
        {
            Assert.NotEmpty(issue.FixToolIds);
            Assert.False(string.IsNullOrWhiteSpace(issue.FixLabel));
        });
        Assert.All(KnownIssues.All.Where(issue => issue.Remedy is RemedyKind.Manual or RemedyKind.Restart), issue =>
            Assert.Empty(issue.FixToolIds));
    }

    [Fact]
    public void Find_returns_the_issue_or_null() =>
        Assert.Equal((KnownIssues.Get(KnownIssues.DiskFull), null), (KnownIssues.Find("disk-full"), KnownIssues.Find("unknown")));

    [Theory]
    [InlineData(0x80070070, KnownIssues.DiskFull)]
    [InlineData(0x8A150105, KnownIssues.DiskFull)]
    [InlineData(0x80072EE7, KnownIssues.NoConnection)]
    [InlineData(0x8A150107, KnownIssues.NoConnection)]
    [InlineData(0x80073712, KnownIssues.ComponentStoreDamaged)]
    [InlineData(0x800F082F, KnownIssues.RestartPending)]
    [InlineData(0x800F081F, KnownIssues.DismSourceUnavailable)]
    [InlineData(0x800F0906, KnownIssues.DismSourceUnavailable)]
    [InlineData(0x800F0907, KnownIssues.DismSourceUnavailable)]
    [InlineData(0x80070002, null)]
    [InlineData(0x80070422, null)]
    public void Codes_every_tool_shares_map_to_an_issue(uint code, string? issue) =>
        Assert.Equal(issue, KnownIssues.FromHResult(unchecked((int)code)));

    [Theory]
    [InlineData(0x80070422, KnownIssues.WuServiceDisabled)]
    [InlineData(0x80070002, KnownIssues.WuCacheDamaged)]
    [InlineData(0x80070003, KnownIssues.WuCacheDamaged)]
    [InlineData(0x80248007, KnownIssues.WuCacheDamaged)]
    [InlineData(0x80246007, KnownIssues.WuCacheDamaged)]
    [InlineData(0x80240016, KnownIssues.WuBusy)]
    [InlineData(0x8024402C, KnownIssues.NoConnection)]
    [InlineData(0x80070070, KnownIssues.DiskFull)]
    [InlineData(0x80240022, null)]
    public void Windows_update_codes_map_to_an_issue(uint code, string? issue) =>
        Assert.Equal(issue, KnownIssues.FromWindowsUpdate(unchecked((int)code)));
}
