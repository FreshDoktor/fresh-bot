using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;
using FreshBot.Models;

namespace FreshBot;

public class TestBot
{
    private DiscordSocketClient? _client;
    private readonly Repository _repository = Repository.getInstance();
    private List<GuildMessage> _messages = new();

   


    public static Task Main()
    {
        return new TestBot().MainAsync();
    }


    private async Task MainAsync()
    {
        var conf = new DiscordSocketConfig()
        {
            GatewayIntents = GatewayIntents.All
        };

        _client = new DiscordSocketClient(conf);
        _client.Log += Log;
        _client.Ready += Client_Ready;
        _client.ReactionAdded += ReactionAdded;
        _client.ReactionRemoved += ReactionRemoved;
        _client.MessageReceived += MessageReceived;
        _client.ReactionsRemovedForEmote += ReactionCleared;

        await _client.LoginAsync(TokenType.Bot, ConfigUtil.GetProperty(ConfigKey.BOT_TOKEN));
        await _client.StartAsync();
        await Task.Delay(-1);
    }


    private static Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    private async Task Client_Ready()
    {
        Console.Write("Request guild message data from Database:");
        try
        {
            _messages = await _repository.GetGuildMessages();
            foreach (var message in _messages)
                message.Reactions = await _repository.GetGuildMessageReactions(message.Id);
            Console.WriteLine(" Successful!");
        }
        catch (Exception)
        {
            Console.WriteLine(" Failure!");
        }
        
        Console.WriteLine("Bot startup finished!");
    }

    private async Task ReactionAdded(Cacheable<IUserMessage, ulong> eventMessage,
        Cacheable<IMessageChannel, ulong> eventChannel, SocketReaction reaction)
    {
        if (PrepareReactionChanged(eventMessage, eventChannel, reaction, out var start, out var result,
                out var groupToAdd)) return;
        if (groupToAdd == 0 || result.Item1)
            return;

        await result.Item2.AddRoleAsync(groupToAdd);

        var end = DateTime.Now;
        Console.WriteLine("Reaction-roll added: " + end.Subtract(start));
    }

    private async Task ReactionRemoved(Cacheable<IUserMessage, ulong> eventMessage,
        Cacheable<IMessageChannel, ulong> eventChannel, SocketReaction reaction)
    {
        // Bot will remove all Reactions from this specific emote and remove the message reaction for this emote if emote added by bot is removed
        if (_client.CurrentUser.Id == reaction.UserId)
        {
            // Reactions so that definitive a reaction is here
            await eventMessage.GetOrDownloadAsync().Result.AddReactionAsync(reaction.Emote);
            await eventMessage.GetOrDownloadAsync().Result.RemoveAllReactionsForEmoteAsync(reaction.Emote);
            return;
        }

        if (PrepareReactionChanged(eventMessage, eventChannel, reaction, out var start, out var result,
                out var groupToRemove) || groupToRemove == 0 || !result.Item1)
            return;

        if (!result.Item1)
            return;

        await result.Item2.RemoveRoleAsync(groupToRemove);
        var end = DateTime.Now;
        Console.WriteLine("Reaction-roll removed: " + end.Subtract(start));
    }

    private async Task MessageReceived(SocketMessage sMessage)
    {
        var message = sMessage.Content;
        if (sMessage.Type == MessageType.Reply && message.StartsWith('?'))
        {
            var parameters = message.Split(' ');
            var ready = false;
            switch (parameters[0])
            {
                case "?addReaction":
                    AddReaction(parameters[1], parameters[2], sMessage);
                    ready = true;
                    break;
            }

            if (ready) await sMessage.DeleteAsync();
        }
    }

    private async Task ReactionCleared(Cacheable<IUserMessage, ulong> eventMessage,
        Cacheable<IMessageChannel, ulong> eventChannel, IEmote emote)
    {
        var gmId = await _repository.GetGMIdByMessageId(eventMessage.Id);
        var emoteId = (emote as Emote).Id;
        await _repository.RemoveMessageReactions(gmId, emoteId);


        var guildMessageReactions = await _repository.GetGuildMessageReactions(eventMessage.Id);
        if (guildMessageReactions.Count > 0)
            return;

        await _repository.RemoveGuildMessage(gmId);
        var guildMessage = _messages.Find(message => message.Id == gmId);
        _messages.Remove(guildMessage);
    }

    private async void AddReaction(string emote, string group, SocketMessage sMessage)
    {
        ulong emoteId = 0;
        ulong groupId = 0;

        if (emote.Length > 17 || Regex.IsMatch(emote, "(<a?)?:\\w+:(\\d{16,20}>)?"))
        {
            var idEmoteIndex = emote.LastIndexOf(':') + 1;
            emoteId = Convert.ToUInt64(emote.Substring(idEmoteIndex, emote.Length - idEmoteIndex - 1));
        }
        else
        {
            Console.Error.WriteLine(sMessage.Author.Username + " tried to add non custom emote!");
            return;
        }

        if (group.Length >= 16)
        {
            var idGroupIndex = group.IndexOf('&') + 1;
            groupId = Convert.ToUInt64(group.Substring(idGroupIndex, group.Length - idGroupIndex - 1));
        }

        var refMessageId = sMessage.Reference.MessageId.Value;


        // Search for guild Message, ig non is found create one in database
        var message = _messages.Find(message => message.MessageId == refMessageId);
        var guildId = (sMessage.Channel as SocketGuildChannel)!.Guild.Id;

        if (message == null)
            await _repository.AddGuildMessage(new GuildMessage(refMessageId, guildId));


        var gMessage = await _repository.GetGuildMessageByMessageId(refMessageId);

        if (message == null) _messages.Add(gMessage);

        await _repository.AddMessageReaction(new MessageReaction(emoteId, groupId), gMessage.Id);

        _messages.Find(guildMessage => guildMessage.Id == gMessage.Id).Reactions
            .Add(new MessageReaction(emoteId, groupId));


        await sMessage.Channel.GetMessageAsync(sMessage.Reference.MessageId.Value).Result
            .AddReactionAsync(Emote.Parse(emote));
    }

    /**
         * <summary>
         * Fills start time as DateTime, groupId to group that should be added and the tuple with an indicator if the
         * user already has the desired group and the user who triggered the event.
         * </summary>
         * Returns true if program should continue.
         */
    private bool PrepareReactionChanged(Cacheable<IUserMessage, ulong> eventMessage,
        Cacheable<IMessageChannel, ulong> eventChannel, SocketReaction reaction,
        out DateTime start, out Tuple<bool, IGuildUser?> result, out ulong groupToAdd)
    {
        start = DateTime.Now;
        groupToAdd = 0;
        result = new Tuple<bool, IGuildUser?>(false, null);

        if (GetReactionEventGuild(eventMessage, eventChannel, out var guild))
            return true;

        var message = _messages.Find(message => message.GuildId == guild.Id && message.MessageId == eventMessage.Id);
        if (message == null)
            return true;

        result = UserHasCorresponsiveRole(reaction.UserId, (reaction.Emote as Emote)!.Id, guild, message);

        groupToAdd = message.GetGroupIdByEmote((reaction.Emote as Emote)!.Id);
        return false;
    }


    /**
         * Get SocketGuild of event if guild id is present in database, returns true if guild is found false if not. 
         */
    private bool GetReactionEventGuild(Cacheable<IUserMessage, ulong> eventMessage,
        Cacheable<IMessageChannel, ulong> eventChannel, out SocketGuild guild)
    {
        // Get all necessary data to check if event can be ignored
        var channel = eventChannel.GetOrDownloadAsync().Result as SocketTextChannel;
        var guildId = channel.Guild.Id;
        var guildMessageId = _messages.Find(botGuild => botGuild.GuildId == guildId)!.MessageId;


        // If the saved channel is not the reaction channel we can ignore the event.
        if (guildMessageId != eventMessage.Id)
        {
            guild = channel.Guild;
            return false;
        }

        guild = _client.GetGuild(guildId);
        return false;
    }


    /**
         * Returns user and true if user got the group corresponsive to the given emote and false if not
         */
    private Tuple<bool, IGuildUser?> UserHasCorresponsiveRole(ulong userId, ulong emoteId, SocketGuild guild,
        GuildMessage guildMessage)
    {
        foreach (var guildUser in guild.GetUsersAsync().FlattenAsync().Result)
        {
            if (guildUser.Id != userId)
                continue;

            return guildUser.RoleIds.Any(rollId => rollId == guildMessage.GetGroupIdByEmote(emoteId))
                ? new Tuple<bool, IGuildUser?>(true, guildUser)
                : new Tuple<bool, IGuildUser?>(false, guildUser);
        }

        return new Tuple<bool, IGuildUser?>(false, null);
    }
}