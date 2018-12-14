using Discord;
using Discord.Gateway;
using System;
using System.Diagnostics;
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
                Console.WriteLine("No auth token found - preparing first run configuration");
                Process p = Process.Start(new ProcessStartInfo("config.json") { UseShellExecute = true });
                p.WaitForExit();

                cfg = Configuration.Read("config.json");
            }

            if (string.IsNullOrWhiteSpace(cfg.AuthToken))
            {
                Trace.WriteLine("No auth token configured - exiting.");
                return;
            }

            Gateway gateway = Discord.Discord.CreateGateway(
                new Discord.Credentials.Credentials(cfg.AuthToken),
                cfg
            );

            Discord.Json.Objects.GetGatewayResponseObject getGateway = await gateway.AuthenticateAsync(cts.Token);
            Console.WriteLine($"Gateway URL: {getGateway.url} - Sessions remaining: "
                + $"{(getGateway as Discord.Json.Objects.GetGatewayBotResponseObject).session_start_limit.remaining}");


            Task blockable = await Connect(gateway, cts.Token, getGateway);

            await blockable;

            while (true)
            {
                blockable = await gateway.ReconnectAsync();

                await blockable;

                Console.WriteLine(blockable.Status);
                Console.WriteLine(blockable.Exception);
            }
        }

        static async Task<Task> Connect(Gateway gateway, CancellationToken token, Discord.Json.Objects.GetGatewayResponseObject response)
        {
            return await gateway.ConnectAsync(token, response);
        }
    }
}
