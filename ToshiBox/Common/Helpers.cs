using System.Diagnostics.CodeAnalysis;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.DalamudServices;

namespace ToshiBox.Common;

public static class Helpers
{
    /// <summary>
    /// Prints a message to chat with a colored "[ToshiBox]" prefix and optional colored message text.
    /// </summary>
    /// <param name="message">The message text to print.</param>
    /// <param name="messageColor">Optional color for the message text. If null, default color is used.</param>
    public static void PrintToshi(string message, byte? messageColor = null)
    {
        var ssb = new SeStringBuilder();
        ssb.AddUiForeground($"[{nameof(ToshiBox)}]", 48); // colored prefix
        ssb.AddText(" ");                       // space separator

        if (messageColor.HasValue)
            ssb.AddUiForeground(message, messageColor.Value);
        else
            ssb.AddText(message);

        Svc.Chat.Print(ssb.Build());
    }
    
    public static void PrintGlowshi(string message, byte? messageColor = null)
    {
        var ssb = new SeStringBuilder();
        ssb.AddUiGlow($"[{nameof(ToshiBox)}]", 48); // colored prefix
        ssb.AddText(" ");                       // space separator

        if (messageColor.HasValue)
            ssb.AddUiForeground(message, messageColor.Value);
        else
            ssb.AddText(message);

        Svc.Chat.Print(ssb.Build());
    }
}
