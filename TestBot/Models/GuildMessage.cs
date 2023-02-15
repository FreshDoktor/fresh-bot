using System.Runtime.InteropServices;
using Discord;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace TestBot.Models;

public class GuildMessage
{
    private ulong _id;
    private ulong _messageId;
    private ulong _guildId;
    private List<MessageReaction> _reactions = new();

    public GuildMessage(ulong id, ulong messageId, ulong guildId)
    {
        _id = id;
        _messageId = messageId;
        _guildId = guildId;
    }

    public GuildMessage(ulong messageId, ulong guildId)
    {
        _messageId = messageId;
        _guildId = guildId;
    }

    public ulong Id => _id;

    public ulong GuildId => _guildId;

    public List<MessageReaction> Reactions
    {
        get => _reactions;
        set => _reactions = value ?? throw new ArgumentNullException(nameof(value));
    }

    public ulong MessageId
    {
        get => _messageId;
        set => _messageId = value;
    }

    public ulong GetGroupIdByEmote(ulong emoteId)
    {
        var messageReaction = _reactions.Find(reaction => reaction.EmoteId == emoteId);
        return messageReaction?.GroupId ?? 0;
    }

    public bool RemoveReactionEmoteId(ulong emoteId)
    {
        List<MessageReaction> messages = _reactions.FindAll(reaction => reaction.EmoteId == emoteId);

        // if none or more than one match is found something is wrong so return false!
        if (!messages.Any() || messages.Count > 1)
            return false;

        return messages.Remove(messages[0]);
    }
}