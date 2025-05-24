using Dalamud.Game.Network.Structures;

namespace ToshiBox.Common;
public class Events
{
    public event Action<uint, uint, uint>? AchievementProgressUpdate;
    public void OnAchievementProgressUpdate(uint id, uint current, uint max) => AchievementProgressUpdate?.Invoke(id, current, max);

    public event Action<nint, nint, nint, byte>? PacketSent;
    public void OnPacketSent(nint addon, nint opcode, nint data, byte result) => PacketSent?.Invoke(addon, opcode, data, result);

    public event Action<nint, uint, nint>? PacketReceived;
    public void OnPacketRecieved(nint addon, uint opcode, nint data) => PacketReceived?.Invoke(addon, opcode, data);

    public event Action? ListingsStart;
    public void OnListingsStart() => ListingsStart?.Invoke();

    public event Action<IReadOnlyList<IMarketBoardItemListing>>? ListingsPage;
    public void OnListingsPage(IReadOnlyList<IMarketBoardItemListing> itemListings) => ListingsPage?.Invoke(itemListings);

    public event Action<List<IMarketBoardItemListing>>? ListingsEnd;
    public void OnListingsEnd(List<IMarketBoardItemListing> listings) => ListingsEnd?.Invoke(listings);

    public event Action? EnteredPvPInstance;
    public void OnEnteredPvPInstance() => EnteredPvPInstance?.Invoke();

    public event Action<DateTime, uint, uint, ushort, uint, Memory<byte>>? ServerIPCReceived;
    public void OnServerIPCReceived(DateTime sendTimestamp, uint sourceServerActor, uint targetServerActor, ushort opcode, uint epoch, Span<byte> payload)
        => ServerIPCReceived?.Invoke(sendTimestamp, sourceServerActor, targetServerActor, opcode, epoch, payload.ToArray().AsMemory());
    public delegate void ServerIPCReceivedDelegate();
}
