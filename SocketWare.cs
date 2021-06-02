using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.Web;

using System.Text;
using HttpContext = Microsoft.AspNetCore.Http.HttpContext;

namespace WebApplication5Sssocket
{
    public class SocketWare
    {

        private readonly RequestDelegate _next;

        BlockingCollection<WebSocket> websockets = new BlockingCollection<WebSocket>();


        public SocketWare(RequestDelegate next)
        {
            Console.WriteLine("Dostalem nexta");
            _next = next;
        }

        public async Task Invoke(HttpContext context, ICustomWebsockerFactory wsFactory, ICustomWEbSocketMessageHandler wsmHandler)
        {
            if (context.Request.Path == "/ws")
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    string username = context.Request.Query["u"];
                    if (!string.IsNullOrEmpty(username))
                    {
                        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                        CustomWebSocket userWebSocket = new CustomWebSocket()
                        {
                            WebSocket = webSocket,
                            Username = username
                        };
                        wsFactory.Add(userWebSocket);
                        await wsmHandler.SendInitialMessages(userWebSocket);
                        await Listen(context, userWebSocket, wsFactory, wsmHandler);
                    }
                }
                else
                {
                    context.Response.StatusCode = 400;
                }
            }
            await _next(context);
        }

        private async Task Listen(HttpContext context, CustomWebSocket userWebSocket, ICustomWebSocketFactory wsFactory, ICustomWebSocketMessageHandler wsmHandler)
        {
            WebSocket webSocket = userWebSocket.WebSocket;
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            while (!result.CloseStatus.HasValue)
            {
                await wsmHandler.HandleMessage(result, buffer, userWebSocket, wsFactory);
                buffer = new byte[1024 * 4];
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            wsFactory.Remove(userWebSocket.Username);
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }
    }




        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Path == "/ws")
            {
                Console.WriteLine("Close messagetype");
            }
            if (context.WebSockets.IsWebSocketRequest)
            {

                WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();

                Console.WriteLine("Socket connected  ");

                await ReceiveMessage(webSocket, async (result, buffer) =>
                {
                    if (!websockets.Contains(webSocket))
                        websockets.Add(webSocket);

                    Console.WriteLine(result.CloseStatus.ToString());

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine("Deleting current socket");
                        websockets.TryTake(out webSocket);

                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        Console.WriteLine(context.Request.Path.ToString());

                        Console.WriteLine(context.Request.Query["user_id"]);
                        var user_id = context.Request.Query["user_id"].ToString();
                        Console.WriteLine(context.Connection.Id.ToString());
                        var lastMessage = Encoding.Default.GetString(buffer);
                        lastMessage = lastMessage.Trim('\0');
                        Console.WriteLine(lastMessage);

                        JsonModel m = new JsonModel(user_id, lastMessage);

                        var data = JsonConvert.SerializeObject(m, Formatting.None);


                        //var data = "{'user_id\" : 123}";


                        foreach (WebSocket socket in websockets)
                        {
                            var encoded = Encoding.UTF8.GetBytes(data);
                            //Console.WriteLine(new ArraySegment<byte>(Encoding.Default.GetBytes(lastMessage)));
                            var tempBuffer = new ArraySegment<Byte>(encoded, 0, encoded.Length);
                            Console.WriteLine("String " + data);
                            Console.WriteLine("String encoded" + encoded);
                            await socket.SendAsync(tempBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
                            //await socket.SendAsync(new ArraySegment<byte>(Encoding.Default.GetBytes(data)), WebSocketMessageType.Text, true, CancellationToken.None);


                        }

                        return;

                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        websockets.TryTake(out webSocket);
                        Console.WriteLine("Close messagetype");
                    }

                });
            }
            else
            {
                Console.WriteLine("Hello from the 2rd request delegate");
                await next();
            }
   




        }

private async Task ReceiveMessage(WebSocket socket, Action<WebSocketReceiveResult, byte[]> handleMessage)
        {
            var buffer = new byte[1024 * 4];

            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer: new ArraySegment<byte>(buffer),
                    cancellationToken: CancellationToken.None);


                handleMessage(result, buffer);
            }
        }
    }

public class JsonModel
{
    public JsonModel(string user_id, string lastMessage)
    {
        this.user_id = user_id;
        this.lastMessage = lastMessage;
    }

    public string user_id { set; get; }
    public string lastMessage { get; set; }
}
}
