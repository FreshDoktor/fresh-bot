using System.Configuration;
using System.Security;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using TestBot.Models;

namespace TestBot;

public class Repository
{
    private bool logDatabaseStatements = false;
    
    private static Repository? instance;

    public static Repository getInstance()
    {
        return instance ??= new Repository();
    }
    
    private Repository()
    {
        Console.WriteLine("Open Database connection");
        var connectionUrl = "Server=" + ConfigUtil.GetProperty(ConfigKey.DB_HOSTNAME) +
                            ";User=" + ConfigUtil.GetProperty(ConfigKey.DB_USERNAME) +
                            ";pwd=" + ConfigUtil.GetProperty(ConfigKey.DB_PASSWORD) +
                            ";Database=" + ConfigUtil.GetProperty(ConfigKey.DB_DATABASE);
        connection = new MySqlConnection(connectionUrl);
        try
        {
            connection.Open();
            Console.WriteLine("Connection to Database established!");
            logDatabaseStatements = ConfigUtil.GetProperty(ConfigKey.LOG_DATABASE);
        }
        catch (MySqlException e)
        {
            Console.Error.WriteLine(e.Message);
        }
    }

    private MySqlConnection connection;

    private async Task<List<object[]>> GetMySqlResponse(string sqlCommand, uint colCount)
    {
        DateTime start, end;
        start = DateTime.Now;

        List<object[]> responseList = new();

        var command = new MySqlCommand(sqlCommand, connection);
        MySqlDataReader reader;
        try
        {
            reader = await command.ExecuteReaderAsync();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Error during database query:");
            Console.Error.WriteLine(e.Message);
            throw e;
        }


        while (await reader.ReadAsync())
        {
            object[] objects = new object[colCount];
            reader.GetValues(objects);
            responseList.Add(objects);
        }

        await reader.CloseAsync();

        end = DateTime.Now;
        
        if (logDatabaseStatements)
            Console.WriteLine("Database Statement\t(" + sqlCommand + ") executed: " + end.Subtract(start));
        return responseList;
    }

   

    public async Task<List<GuildMessage>> GetGuildMessages()
    {
        List<GuildMessage> messages = new();
        var response = await GetMySqlResponse("SELECT * FROM GUILD_MESSAGES", 3);
        foreach (var obj in response) messages.Add(new GuildMessage((ulong)obj[2], (ulong)obj[1], (ulong)obj[0]));

        return messages;
    }

    public async Task<List<MessageReaction>> GetGuildMessageReactions(ulong gmId)
    {
        List<MessageReaction> reactions = new();
        var sqlCommand = $"SELECT * FROM MESSAGE_REACTIONS WHERE GM_ID = {gmId}";
        var response = await GetMySqlResponse(sqlCommand, 3);

        foreach (var obj in response)
            reactions.Add(
                new MessageReaction(
                    (ulong)obj[1],
                    (ulong)obj[2]
                )
            );

        return reactions;
    }

    public async Task AddGuildMessage(GuildMessage guildMessage)
    {
        var sqlCommand =
            $"INSERT INTO GUILD_MESSAGES(GUILD_ID, MESSAGE_ID) VALUES ({guildMessage.GuildId},{guildMessage.MessageId})";
        await GetMySqlResponse(sqlCommand, 0);
    }

    public async Task AddMessageReaction(MessageReaction messageReaction, ulong gmId)
    {
        var sqlCommand =
            $"INSERT INTO MESSAGE_REACTIONS(GM_ID, EMOTE_ID, GROUP_ID) VALUES ({gmId},{messageReaction.EmoteId},{messageReaction.GroupId})";
        await GetMySqlResponse(sqlCommand, 0);
    }

    public async Task<GuildMessage> GetGuildMessageByMessageId(ulong refMessageId)
    {
        var sqlCommand = $"SELECT * FROM GUILD_MESSAGES WHERE MESSAGE_ID = {refMessageId}";

        List<object[]> response = await GetMySqlResponse(sqlCommand, 3);

        if (response.Count > 1)
        {
            // TODO Exception - There should only one element!
        }

        return new GuildMessage((ulong)response[0][2], (ulong)response[0][1], (ulong)response[0][0]);
    }

    public async Task<ulong> GetGMIdByMessageId(ulong messageId)
    {
        var sqlCommand = $"SELECT ID FROM GUILD_MESSAGES WHERE MESSAGE_ID = {messageId}";
        var response = await GetMySqlResponse(sqlCommand, 1);

        if (response.Count <= 0)
            return 0;

        return (ulong)response[0][0];
    }

    public async Task RemoveMessageReactions(ulong gmId, ulong emoteId)
    {
        var sqlCommand = $"DELETE FROM MESSAGE_REACTIONS WHERE GM_ID = {gmId} AND EMOTE_ID = {emoteId}";
        await GetMySqlResponse(sqlCommand, 0);
    }

    public async Task RemoveGuildMessage(ulong gmId)
    {
        var sqlCommand = $"DELETE FROM GUILD_MESSAGES WHERE ID = {gmId}";
        await GetMySqlResponse(sqlCommand, 0);
    }
}