using Discord;
using Discord.Gateway;
using MapCord;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Skedaddle
{
    class Program
    {
        static Mapper mapper;
        static Gateway gateway;
        static CancellationTokenSource cts = new CancellationTokenSource();
        static Task blockable;
        static bool continueReconnecting = true;

        static async Task Main(string[] args)
        {
            Configuration cfg = Configuration.Load("config.json");

            if (string.IsNullOrWhiteSpace(cfg.AuthToken))
            {
                Console.WriteLine("No auth token found - preparing first run configuration");
                Process p = Process.Start(new ProcessStartInfo("config.json") { UseShellExecute = true });
                p.WaitForExit();

                cfg = Configuration.Load("config.json");
            }

            if (string.IsNullOrWhiteSpace(cfg.AuthToken))
            {
                Trace.WriteLine("No auth token configured - exiting.");
                return;
            }

            gateway = Discord.Discord.CreateGateway(
                new Discord.Credentials.Credentials(cfg.AuthToken),
                cfg
            );

            Discord.Json.Objects.GetGatewayResponseObject getGateway = await gateway.AuthenticateAsync(cts.Token);
            Console.WriteLine($"Gateway URL: {getGateway.url} - Sessions remaining: "
                + $"{(getGateway as Discord.Json.Objects.GetGatewayBotResponseObject).session_start_limit.remaining}");

            mapper = new Mapper(gateway);

            gateway.Events.AddAsyncEventCallback<Discord.Descriptors.Channels.MessageDescriptor>(Discord.Enums.EventType.MESSAGE_CREATE, OnMessage);
            gateway.LoadPreviousSession();

            try
            {
                blockable = await gateway.ConnectAsync(cts.Token, getGateway);
                await blockable;
            }
            catch
            {

            }

            int reconnCount = 1;

            while (continueReconnecting)
            {
                try
                {
                    Console.WriteLine("Reconnecting... #" + reconnCount);
                    cts = new CancellationTokenSource();
                    blockable = await gateway.ConnectAsync(cts.Token);
                    await blockable;
                }
                catch
                {

                }
                reconnCount++;
            }

            cfg.Save();
        }

        const string DeathbulgeUrl = "http://deathbulge.com/api/comics";
        static int? _randomComic = null;

        static async Task OnMessage(string json, Discord.Descriptors.DispatchGatewayEvent<Discord.Descriptors.Channels.MessageDescriptor> e)
        {
            mapper.Map(e.Payload);


            if (e.Payload.Content == "$$restart")
            {
                await gateway.DisconnectAsync(System.Net.WebSockets.WebSocketCloseStatus.Empty);
            }
            else if (e.Payload.Content == "$$stop")
            {
                continueReconnecting = false;
                await gateway.DisconnectAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure);
            }
            else if (e.Payload.Content.StartsWith("$$deathbulge"))
            {
                DeathbulgeData data;
                if (_randomComic == null)
                {
                    data = await gateway.Rest.GetAsync<DeathbulgeData>(DeathbulgeUrl, null, cts.Token);
                    _randomComic = data.pagination_links.random;
                }

                data = await gateway.Rest.GetAsync<DeathbulgeData>(DeathbulgeUrl + $"/{_randomComic.Value}", null, cts.Token);
                _randomComic = data.pagination_links.random;
                try
                {
                    await gateway.Rest.Channels.PostMessageAsync(
                        e.Payload.Channel,
                        new Discord.Descriptors.Channels.MessageDescriptor
                        {
                            embeds = new[]
                            {
                                    new Discord.Json.Objects.Channels.Embeds.EmbedObject
                                    {
                                        title = data.HeaderString,
                                        url = $"http://deathbulge.com{data.comic.comic}",
                                        image = new Discord.Json.Objects.Channels.Embeds.EmbedImageObject
                                        {
                                            url = $"http://deathbulge.com{data.comic.comic}"
                                        },
                                        footer = new Discord.Json.Objects.Channels.Embeds.EmbedFooterObject
                                        {
                                            text = data.comic.AltString
                                        },
                                        timestamp = data.comic.timestamp,
                                    }
                            }
                        },
                        cts.Token
                    );
                }
                catch (HttpRequestException ex)
                {
                    Trace.TraceError(ex.Message);
                }
            }
        }
    }

    internal class DeathbulgeData
{
    public DeathbulgeComic comic;
    public DeathbulgePagination pagination_links;
    public bool is_first;
    public bool is_last;
    public int Id => is_first ? pagination_links.first
                   : is_last ? pagination_links.last
                   : pagination_links.next - 1;
    public string HeaderString => $"Deathbulge #{Id} - {comic.ReplaceCharacterCodes(comic.title)}";
}

internal class DeathbulgePagination
{
    public int first;
    public int last;
    public int next;
    public int previous;
    public int random;
}

internal class DeathbulgeComic
{
    public int id;
    public string title;
    public string comic;
    public DateTime timestamp;
    public string alt_text;

    public string AltString => ReplaceCharacterCodes(alt_text);

    public string ReplaceCharacterCodes(string str)
    {
        str = str.Replace("&eacute;", "é");
        str = str.Replace("&egrave;", "è");

        return str;
    }
}
}
