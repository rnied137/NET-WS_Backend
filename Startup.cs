using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;
using System.Threading;
using System.Text;
using System.Collections.Concurrent;
using Newtonsoft.Json;

namespace WebApplication5Sssocket
{
    public class Startup
    {
        BlockingCollection<WebSocket> websockets = new BlockingCollection<WebSocket>();

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(options => {
                options.AddPolicy("CorsPolicy", builder => {
                    builder.AllowAnyMethod().AllowAnyOrigin().AllowAnyHeader();
                });
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseWebSockets();
            app.UseCors("CorsPolicy");
            app.Use(async (context, next) =>

            {
                if(context.Request.Path == "/ws")
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

                        if(result.MessageType == WebSocketMessageType.Close)
                        {
                            Console.WriteLine("Deleting current socket");
                             websockets.TryTake(out webSocket);

                        }

                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            Console.WriteLine(context.Request.Path.ToString());

                            Console.WriteLine(context.Request.Query["user_id"]) ;
                            var user_id = context.Request.Query["user_id"].ToString();
                            Console.WriteLine(context.Connection.Id.ToString());
                            var lastMessage = Encoding.Default.GetString(buffer);
                            lastMessage = lastMessage.Trim('\0');
                            Console.WriteLine(lastMessage);

                            JsonModel m = new JsonModel(user_id, lastMessage);

                            var data = JsonConvert.SerializeObject(m,Formatting.None);


                            //var data = "{'user_id\" : 123}";


                            foreach (WebSocket socket  in websockets)
                            {
                                var encoded = Encoding.UTF8.GetBytes(data);
                                //Console.WriteLine(new ArraySegment<byte>(Encoding.Default.GetBytes(lastMessage)));
                                var tempBuffer = new ArraySegment<Byte>(encoded, 0, encoded.Length);
                                Console.WriteLine("String " + data);
                                Console.WriteLine("String encoded" + encoded);
                                await socket.SendAsync(tempBuffer,WebSocketMessageType.Text, true, CancellationToken.None);
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
            });

            app.Run(async context =>
            {
                Console.WriteLine("Hello from the 3rd request delegate");
                await context.Response.WriteAsync("Hello once again");
            });


           

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
