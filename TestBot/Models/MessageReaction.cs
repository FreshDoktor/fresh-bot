namespace TestBot.Models;

public class MessageReaction
{
    private ulong _emoteId;
    private ulong _groupId;

    public MessageReaction(ulong emoteId, ulong groupId)
    {
        _emoteId = emoteId;
        _groupId = groupId;
    }

    public ulong EmoteId
    {
        get => _emoteId;
        set => _emoteId = value;
    }

    public ulong GroupId
    {
        get => _groupId;
        set => _groupId = value;
    }
}