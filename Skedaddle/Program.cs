using Discord;
using Discord.Gateway;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Skedaddle
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Configuration cfg = Configuration.Read("config.json");
            CancellationTokenSource cts = new CancellationTokenSource();

            if (string.IsNullOrWhiteSpace(cfg.AuthToken))
            {
                throw new InvalidOperationException("A valid auth token must be configured");
            }

            Gateway gateway = Discord.Discord.CreateGateway(
                new Discord.Credentials.Credentials(cfg.AuthToken),
                cfg
            );

            Discord.Json.Objects.GetGatewayResponseObject getGateway = await gateway.AuthenticateAsync(cts.Token);
            Console.WriteLine($"Gateway URL: {getGateway.url}");
            Task blockable = await gateway.ConnectAsync(cts.Token, getGateway);

            await blockable;

            Console.ReadKey();
        }
    }
}
