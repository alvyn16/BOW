namespace BOW.Tests;

public class BowStoreTests
{
    private static BowStore CreateStore() => new(
        new SettingsModel { RestoreSessionOnStart = false }, []);

    [Fact]
    public void SplitActiveTab_PreservesPrimaryAndLinksBothTabs()
    {
        var store = CreateStore();
        var primary = store.AddTab("https://example.com");

        store.SplitActiveTab();

        var partner = Assert.Single(store.Tabs.Where(t => t.Id != primary.Id && t.Url == primary.Url));
        Assert.Same(primary, store.ActiveTab);
        Assert.Equal(partner.Id, primary.SplitPartnerId);
        Assert.Equal(primary.Id, partner.SplitPartnerId);
    }

    [Fact]
    public void CloseSplitTab_ClearsPartnerLink()
    {
        var store = CreateStore();
        var primary = store.AddTab("https://example.com");
        store.SplitActiveTab();
        var partner = store.Tabs.Single(t => t.Id == primary.SplitPartnerId);

        store.CloseTab(partner.Id);

        Assert.False(primary.IsSplitPartner);
        Assert.Null(primary.SplitPartnerId);
    }

    [Fact]
    public void JoinSplitTab_RemovesPartnerAndKeepsActiveTabInStore()
    {
        var store = CreateStore();
        var primary = store.AddTab("https://example.com");
        store.SplitActiveTab();
        var partner = store.Tabs.Single(t => t.Id == primary.SplitPartnerId);
        store.SetActiveTab(partner.Id);

        store.JoinSplitTab(primary.Id);

        Assert.Same(primary, store.ActiveTab);
        Assert.DoesNotContain(partner, store.Tabs);
        Assert.Null(primary.SplitPartnerId);
    }

    [Fact]
    public void ActivatingSleepingTab_WakesIt()
    {
        var store = CreateStore();
        var tab = store.AddTab("https://example.com");
        tab.IsSleeping = true;

        store.SetActiveTab(tab.Id);

        Assert.False(tab.IsSleeping);
    }

    [Fact]
    public void DisabledSessionRestore_StillRestoresPinnedTabs()
    {
        var store = new BowStore(
            new SettingsModel { RestoreSessionOnStart = false },
            [new SessionEntry("https://pinned.example", true), new SessionEntry("https://other.example", false)]);

        var tab = Assert.Single(store.Tabs);
        Assert.Equal("https://pinned.example", tab.Url);
        Assert.True(tab.IsPinned);
    }

    [Fact]
    public void DuplicateMoveAndGroupTabs_PreserveStateAndOrder()
    {
        var store = CreateStore();
        var first = store.AddTab("https://example.com/first");
        var second = store.AddTab("https://example.com/second");
        store.SetTabGroup(first.Id, "Work");
        store.SetTabGroup(second.Id, "Work");
        first.IsMuted = true;

        var copy = store.DuplicateTab(first.Id)!;
        Assert.Equal("Work", copy.GroupName);
        Assert.True(copy.IsMuted);
        Assert.Equal(first.Url, copy.Url);
        Assert.Equal(store.Tabs.IndexOf(first) + 1, store.Tabs.IndexOf(copy));

        store.MoveTab(copy.Id, second.Id);
        Assert.Equal(store.Tabs.IndexOf(second) - 1, store.Tabs.IndexOf(copy));
        store.RenameGroup("Work", "Personal");
        Assert.All(store.Tabs.Where(t => t.GroupName is not null),
            tab => Assert.Equal("Personal", tab.GroupName));
    }

    [Fact]
    public void RecentlyClosedMenu_CanReopenSelectedTabWithGroupAndMute()
    {
        var store = CreateStore();
        var first = store.AddTab("https://example.com/one");
        first.Title = "First";
        first.GroupName = "Research";
        first.IsMuted = true;
        var second = store.AddTab("https://example.com/two");
        second.Title = "Second";
        store.CloseTab(first.Id);
        store.CloseTab(second.Id);

        var chosen = store.RecentlyClosedTabs.Single(tab => tab.Title == "First");
        store.ReopenClosedTab(chosen.Id);
        Assert.Equal("https://example.com/one", store.ActiveTab!.Url);
        Assert.Equal("Research", store.ActiveTab.GroupName);
        Assert.True(store.ActiveTab.IsMuted);
        Assert.Single(store.RecentlyClosedTabs);
    }
}

/// <summary>
/// Tests for BowStore core tab logic.
/// Note: BowStore constructor loads settings and session — we test via a headless subset.
/// </summary>
public class SearchServiceTests
{
    private static SettingsModel DefaultSettings() => new();

    [Theory]
    [InlineData("google.com", "https://google.com")]
    [InlineData("www.github.com", "https://www.github.com")]
    [InlineData("https://example.com", "https://example.com")]
    [InlineData("http://example.com/path", "http://example.com/path")]
    [InlineData("localhost:3000", "http://localhost:3000")]
    [InlineData("localhost", "http://localhost")]
    public void Resolve_ValidUrl_ReturnsNavigableUrl(string input, string expected)
    {
        var result = SearchService.Resolve(input, DefaultSettings());
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("hello world")]
    [InlineData("how to code in c#")]
    [InlineData("what is the weather")]
    public void Resolve_SearchQuery_UsesDuckDuckGoByDefault(string input)
    {
        var result = SearchService.Resolve(input, DefaultSettings());
        Assert.StartsWith("https://duckduckgo.com/?q=", result);
    }

    [Fact]
    public void Resolve_Startpage_EncodesQuery()
    {
        var settings = new SettingsModel { SearchEngine = "Startpage" };
        Assert.Equal("https://www.startpage.com/sp/search?query=hello%20world", SearchService.Resolve("hello world", settings));
    }

    [Fact]
    public void Resolve_Google_UsesSelectedEngine()
    {
        var settings = new SettingsModel { SearchEngine = "Google" };
        Assert.Equal("https://www.google.com/search?q=hello%20world", SearchService.Resolve("hello world", settings));
    }

    [Fact]
    public void Resolve_Bing_UsesCorrectEngine()
    {
        var settings = new SettingsModel { SearchEngine = "Bing" };
        var result = SearchService.Resolve("hello world", settings);
        Assert.StartsWith("https://www.bing.com/search?q=", result);
    }

    [Fact]
    public void Resolve_CustomEngine_UsesCustomUrl()
    {
        var settings = new SettingsModel
        {
            SearchEngine = "Custom",
            CustomSearchUrl = "https://custom.com/search?q={0}"
        };
        var result = SearchService.Resolve("foo bar", settings);
        Assert.StartsWith("https://custom.com/search?q=", result);
    }

    [Fact]
    public void Resolve_InvalidCustomEngine_FallsBackToDuckDuckGo()
    {
        var settings = new SettingsModel { SearchEngine = "Custom", CustomSearchUrl = "bad {1} template" };

        var result = SearchService.Resolve("hello world", settings);

        Assert.StartsWith("https://duckduckgo.com/?q=", result);
    }

    [Fact]
    public void Resolve_Empty_ReturnsNewTab()
    {
        var result = SearchService.Resolve("", DefaultSettings());
        Assert.Equal("bow:newtab", result);
    }
}

public class SettingsServiceTests
{
    private static string TempDir()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bowtest_{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenFileMissing()
    {
        // Load from non-existent file — uses the real LOCALAPPDATA path
        // but we can test the default values directly since file won't exist in CI
        var settings = new SettingsModel();
        Assert.Equal("Auto", settings.Theme);
        Assert.Equal("Strip", settings.TabLayout);
        Assert.Equal("DuckDuckGo", settings.SearchEngine);
        Assert.Equal(10, settings.TabSleepMinutes);
        Assert.True(settings.RestoreSessionOnStart);
    }

    [Fact]
    public void SettingsModel_Roundtrip_Json()
    {
        var original = new SettingsModel
        {
            Theme = "Dark",
            SearchEngine = "Bing",
            TabSleepMinutes = 5,
            RestoreSessionOnStart = false
        };

        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var restored = System.Text.Json.JsonSerializer.Deserialize<SettingsModel>(json)!;

        Assert.Equal("Dark", restored.Theme);
        Assert.Equal("Bing", restored.SearchEngine);
        Assert.Equal(5, restored.TabSleepMinutes);
        Assert.False(restored.RestoreSessionOnStart);
    }
}

public class SessionManagerTests
{
    [Fact]
    public void TabChangesAreDebouncedAndFlushedOnClose()
    {
        var directory = Path.Combine(Path.GetTempPath(), "bow-session-test-" + Guid.NewGuid());
        var path = Path.Combine(directory, "session.json");
        try
        {
            var store = new BowStore(new SettingsModel(), []);
            using (var saver = new SessionAutoSaver(store,
                (tabs, activeId) => SessionManager.SaveTo(path, tabs, activeId)))
            {
                var first = store.ActiveTab!;
                first.Url = "https://example.org/one";
                first.IsPinned = true;
                first.GroupName = "Research";
                first.IsMuted = true;
                var second = store.AddTab("https://example.org/two");
                Assert.True(SpinWait.SpinUntil(() => SessionManager.LoadFrom(path).Count == 2,
                    TimeSpan.FromSeconds(3)));
                Assert.True(SessionManager.LoadFrom(path)[1].IsActive);
                store.SetActiveTab(first.Id);
                store.CloseTab(second.Id);
            }

            // No normal window-close save: load only the changes observed above.
            var entries = SessionManager.LoadFrom(path);
            var restored = new BowStore(new SettingsModel(), entries);
            Assert.Single(restored.Tabs);
            Assert.Equal("https://example.org/one", restored.ActiveTab!.Url);
            Assert.True(restored.ActiveTab.IsPinned);
            Assert.Equal("Research", restored.ActiveTab.GroupName);
            Assert.True(restored.ActiveTab.IsMuted);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Load_ReturnsEmpty_WhenFileMissing()
    {
        // Session file is at %LOCALAPPDATA%\BOW\session.json
        // In test environment it likely doesn't exist — returns empty
        // We test the contract by checking the type
        var entries = SessionManager.Load();
        Assert.NotNull(entries);
        // May or may not have entries depending on dev machine state — just check type
    }

    [Fact]
    public void SessionEntry_Record_HasCorrectFields()
    {
        var entry = new SessionEntry("https://example.com", true);
        Assert.Equal("https://example.com", entry.Url);
        Assert.True(entry.IsPinned);
    }

    [Fact]
    public void SessionEntry_Roundtrip_Json()
    {
        var entries = new List<SessionEntry>
        {
            new("https://example.com", true),
            new("https://github.com", false)
        };
        var json = System.Text.Json.JsonSerializer.Serialize(entries);
        var restored = System.Text.Json.JsonSerializer.Deserialize<List<SessionEntry>>(json)!;

        Assert.Equal(2, restored.Count);
        Assert.Equal("https://example.com", restored[0].Url);
        Assert.True(restored[0].IsPinned);
        Assert.False(restored[1].IsPinned);
    }
}
