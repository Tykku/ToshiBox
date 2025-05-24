using ECommons.DalamudServices;

namespace ToshiBox.Features.Test;

public class Hello
{
    public Hello()
    {
        Svc.Chat.Print("Hello Test");
    }
}