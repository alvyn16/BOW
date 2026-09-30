using Microsoft.Web.WebView2.Core;

namespace BOW.Services;

public static class TrackingProtectionService
{
    public static void Apply(CoreWebView2 core, string level)
    {
        core.Profile.PreferredTrackingPreventionLevel = level switch
        {
            "Off" => CoreWebView2TrackingPreventionLevel.None,
            "Basic" => CoreWebView2TrackingPreventionLevel.Basic,
            "Strict" => CoreWebView2TrackingPreventionLevel.Strict,
            _ => CoreWebView2TrackingPreventionLevel.Balanced
        };
    }
}
