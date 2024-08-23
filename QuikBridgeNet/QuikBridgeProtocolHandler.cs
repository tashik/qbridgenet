using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuikBridgeNet.Entities;
using QuikBridgeNet.Entities.CommandData;
using QuikBridgeNet.Events;
using Serilog;

namespace QuikBridgeNet;

public class QuikBridgeProtocolHandler
{
    private Socket? _clientSocket;
    private readonly byte[] _buffer = new byte[1024];
    private StringBuilder _accumulatedData = new();

    private bool _isStopped;
    
    private const string MsgTypeReq = "req";
    private const string MsgTypeResp = "ans";
    private const string MsgTypeEnd = "end";
    private const string MsgTypeVersion = "ver";
    
    private readonly QuikBridgeEventDispatcher _eventDispatcher;

    public QuikBridgeProtocolHandler(QuikBridgeEventDispatcher eventDispatcher)
    {
        _eventDispatcher = eventDispatcher;
    }

    public async Task StartClientAsync(string host, int port, CancellationToken cancellationToken)
    {
        try
        {
            _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await _clientSocket.ConnectAsync(host, port, cancellationToken);
            Log.Information("Connected to server at {ServerIP}:{Port}", host, port);

            _isStopped = false;
            _ = Task.Run(() => ReceiveDataAsync(cancellationToken), cancellationToken);

        }
        catch (Exception e)
        {
            Log.Error(e, "Error in StartClientAsync");
        }
    }

    public async Task SendReqAsync(JsonReqMessage req)
    {
        if (_clientSocket == null || !_clientSocket.Connected)
        {
            throw new Exception("Connection problem");
        }
        var reqJson = "{\"id\":" + req.id + ", \"type\":\"" + req.type + "\",\"data\":{";
        reqJson += "\"method\":\"" + req.data.method + "\",";

        if (req.data is JsonReqData commandData)
        {
            reqJson += "\"function\":\"" + commandData.function + "\",";
            if (commandData.arguments is { Length: > 0 })
            {
                reqJson += "\"arguments\":[" + JsonConvert.SerializeObject(commandData.arguments) + "],";
            }
            else
            {
                reqJson += "\"arguments\":[],";
            }

            if (commandData.obj != null)
            {
                reqJson += "\"object\":\"" + commandData.obj + "\",";
            }
        }

        if (req.data is JsonCommandDataSecurity secData)
        {
            if (secData.cl != "")
            {
                reqJson += "\"class\":\"" + secData.cl + "\",";
            }
            if (secData.cl != "")
            {
                reqJson += "\"security\":\"" + secData.security + "\",";
            }
        }

        if (req.data is JsonCommandDataSubscribeParam paramData)
        {
            if (paramData.param != "")
            {
                reqJson += "\"param\":\"" + paramData.param + "\",";
            }
        }

        if (reqJson.EndsWith(","))
        {
            reqJson = reqJson.Remove(reqJson.Length - 1);
        }
        reqJson += "}}";
        Log.Debug($"REQ: {reqJson}");
        await SendMessageAsync(reqJson);
    }

    private async Task SendMessageAsync(string message)
    {
        try
        {
            if (_clientSocket is { Connected: true })
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                await _clientSocket.SendAsync(data, SocketFlags.None);
                Log.Information("Message sent: {Message}", message);
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Error in SendMessageAsync");
        }
    }


    private async Task ReceiveDataAsync(CancellationToken cancellationToken)
    {
        if (_clientSocket == null)
        {
            _isStopped = true;
            throw new Exception("Socket is inactive");
        }

        try
        {
            while (!_isStopped && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    int bytesRead = await _clientSocket.ReceiveAsync(new ArraySegment<byte>(_buffer), SocketFlags.None);

                    if (bytesRead > 0)
                    {
                        string receivedData = Encoding.UTF8.GetString(_buffer, 0, bytesRead);
                        _accumulatedData.Append(receivedData);

                        await ProcessBuffer();
                    }
                    else
                    {
                        if (!_clientSocket.Connected)
                        {
                            Log.Warning("Connection closed by the server.");
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error("Error in ReceiveDataAsync Processing: {0}", e.Message);
                }
            }
        } catch (OperationCanceledException)
        {
            Log.Warning("Receiving canceled.");
        }
        catch (Exception e)
        {
            Log.Error("Error in ReceiveDataAsync: {0}", e.Message);
        }
    }

    private async Task OnDataReceived(JObject jDoc)
    {
        if (jDoc["type"] == null)
        {
            Log.Warning("Unprocessable message format");
            return;
        }
        
        var id = jDoc["id"] == null ? -1 : jDoc["id"]!.ToObject<int>();
        var mtype = jDoc["type"]!.ToString();
        if (id >= 0)
        {
            switch (mtype)
            {
                case MsgTypeEnd:
                {
                    await _eventDispatcher.DispatchAsync(new SocketConnectionCloseEvent());
                    return;
                }
                case MsgTypeVersion:
                {
                    var ver = jDoc["ver"]?.ToObject<int>() ?? 0;
                    VerArrived(ver);
                    return;
                }
                case MsgTypeReq:
                case MsgTypeResp:
                {
                    var data = jDoc["data"] ?? null;
                    await OnNewMessage(id, mtype, data);
                    return;
                }
                default:
                    Console.WriteLine("Unknown message type {0}", mtype);
                    return;
            }
        }
    }
    
    private void VerArrived(int ver)
    {
        Log.Debug($"Version of protocol: {ver}");
    }
    
    private async Task OnNewMessage(int id, string type, JToken? data)
    {
        JsonMessage jReq = new JsonMessage()
        {
            id = id,
            type = type,
            body = data
        };
        switch (type)
        {
            case MsgTypeReq:
                await _eventDispatcher.DispatchAsync(new ReqArrivedEvent(jReq));
                break;
            case MsgTypeResp:
                await _eventDispatcher.DispatchAsync(new RespArrivedEvent(jReq));
                break;
            default:
                Console.WriteLine("Unsupported message type");
                break;
        }
    }

    private async Task ProcessBuffer()
    {
        string accumulatedString = _accumulatedData.ToString();

        while (true)
        {
            try
            {
                JObject? jDoc = TryParseJson(accumulatedString);

                if (jDoc == null)
                {
                    // No complete JSON object, continue accumulating data
                    break;
                }

                // Process the complete JSON object
                Log.Debug("RESP: " + accumulatedString);
                await OnDataReceived(jDoc);
                

                // Remove processed JSON from the buffer
                int jsonEndIndex = FindEndOfJson(accumulatedString);
                accumulatedString = accumulatedString.Substring(jsonEndIndex + 1);
                _accumulatedData = new StringBuilder(accumulatedString);
            }
            catch (JsonReaderException)
            {
                // If parsing fails, it likely indicates an incomplete JSON object
                break;
            }
        }
    }

    private JObject? TryParseJson(string data)
    {
        try
        {
            int jsonEndIndex = FindEndOfJson(data);
            if (jsonEndIndex == -1)
            {
                return null; // Incomplete JSON
            }

            var jsonString = data[..(jsonEndIndex + 1)];
            return JObject.Parse(jsonString);
        }
        catch (JsonReaderException)
        {
            return null; // Incomplete JSON
        }
    }

    private int FindEndOfJson(string data)
    {
        int openBraces = 0;

        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == '{')
            {
                openBraces++;
            }
            else if (data[i] == '}')
            {
                openBraces--;

                if (openBraces == 0)
                {
                    return i;
                }
            }
        }

        return -1; // No complete JSON found
    }
    
    public void Finish()
    {
        JsonCommandRequest req = new JsonCommandRequest
        {
            id = 0,
            type = "end"
        };

        var reqJson = JsonConvert.SerializeObject(req);
        _clientSocket?.Send(Encoding.UTF8.GetBytes(reqJson));
    }
    
    public void StopClient()
    {
        try
        {
            _isStopped = true;
            _clientSocket?.Close();
            Log.Information("Client stopped.");
        }
        catch (Exception e)
        {
            Log.Error(e, "Error in StopClient");
        }
    }
}