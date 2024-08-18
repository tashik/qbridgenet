using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using QuikBridgeNet.Entities;

namespace QuikBridgeNet;

public class JsonProtocolHandler
{
    private Socket? _sock;
    private int _bufSz = 256;
    private int _filledSz;
    private byte[] _incomingBuf = new byte[256];
    private bool _peerEnded;
    private bool _weEnded;
    private ManualResetEvent _responseReceived = new ManualResetEvent(false);


    private int _attempts;

    public const string MsgTypeReq = "req";
    public const string MsgTypeResp = "ans";
    public const string MsgTypeEnd = "end";
    public const string MsgTypeVersion = "ver";

    public JsonProtocolHandler(Socket socket)
    {
        _sock = socket;
        _attempts = 0;
    }

    public bool Connected => _sock is { Connected: true };

    public void Connect(string host, int port)
    {
        IPHostEntry ipHostInfo = Dns.GetHostEntry(host);
        IPAddress? ipAddress = null;
        if (ipHostInfo.AddressList.Length == 0)
        {
            Console.WriteLine("Couldn't resolve ip");
            ipAddress = IPAddress.Parse(host.Trim());
        }
        else
        {
            Console.WriteLine($"{host} resolved to {ipHostInfo.AddressList.Length} addresses");
            int i;
            bool resolved = false;
            for (i = 0; i < ipHostInfo.AddressList.Length; i++)
            {
                if (ipHostInfo.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
                {
                    Console.WriteLine("Address #{0} has correct family", i);
                    ipAddress = ipHostInfo.AddressList[i];
                    resolved = true;
                    break;
                }
            }
            if (!resolved)
            {
                Console.WriteLine("Couldn't resolve ip");
                ipAddress = IPAddress.Parse(host.Trim());
            }
        }

        if (ipAddress == null)
        {
            throw new Exception("Can not parse host address");
        }
        Console.WriteLine("Create socket {0}:{1}", ipAddress, port);
        _sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Console.WriteLine("Connect socket {0}:{1}", ipAddress, port);
        try
        {
            _sock.Connect(ipAddress, port);
            if (!_sock.Connected) return;
            Console.WriteLine("Socket connected");
            var state = new StateObject
            {
                WorkSocket = _sock,
                ProtocolHandler = this
            };
            _sock.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, ReceiveCallback, state);
        }
        catch (SocketException e)
        {
            if (!_sock.Connected)
            {
                _weEnded = true;
            }
            Console.WriteLine(e.Message);
        }
    }

    /// <summary>
    /// Коллбэк-парсер сообщения
    /// </summary>
    /// <param name="ar"></param>
    private static void ReceiveCallback(IAsyncResult ar)
    {
        try
        {
            StateObject? state = (StateObject?) ar.AsyncState;
            Socket? sock = state?.WorkSocket;
            JsonProtocolHandler? ph = state?.ProtocolHandler;

            int? bytesRead = sock?.EndReceive(ar);

            if (bytesRead is not > 0 || state == null || ph == null) return;
            if (ph._bufSz - ph._filledSz < bytesRead)
            {
                var narr = new byte[(int)(ph._filledSz + bytesRead)];
                ph._incomingBuf.CopyTo(narr, 0);
                ph._incomingBuf = narr;
                ph._bufSz = (int)(ph._filledSz + bytesRead);
            }
            Buffer.BlockCopy(state.Buffer, 0, ph._incomingBuf, ph._filledSz, (int) bytesRead);
            ph._filledSz += (int) bytesRead;
            ph.ProcessBuffer();
            sock?.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, ReceiveCallback, state);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    /// <summary>
    /// Parse JSON data
    /// </summary>
    private void ProcessBuffer()
    {
        int i;
        //Console.WriteLine("INCOMMING BUFFER: ");
        //Console.WriteLine(Encoding.UTF8.GetString(_incommingBuf));
        for (i = 0; i < _filledSz; i++)
        {
            if (_incomingBuf[i] == '{')
            {
                if (i > 0)
                {
                    byte[] trash = new byte[i];
                    Buffer.BlockCopy(_incomingBuf, 0, trash, 0, i);
                    if (_filledSz - i > 0)
                        Buffer.BlockCopy(_incomingBuf, i, _incomingBuf, 0, _filledSz - i);
                    _filledSz -= i;
                    Console.WriteLine("Malformed request when buffer copy");
                    ParseError(trash);
                    i = 0;
                }
                break;
            }
        }
        if (i > 0)
            return;

        bool inString = false;
        bool inEsc = false;
        int braceNestingLevel = 0;

        for (i = 0; i < _filledSz; i++)
        {
            byte currCh = _incomingBuf[i];

            if (currCh == '"' && !inEsc)
            {
                inString = !inString;
                continue;
            }

            if (!inString)
            {
                if (currCh == '{')
                    braceNestingLevel++;
                else if (currCh == '}')
                {
                    braceNestingLevel--;
                    if (braceNestingLevel == 0)
                    {
                        byte[] pdoc = new byte[i + 1];
                        Buffer.BlockCopy(_incomingBuf, 0, pdoc, 0, i + 1);
                        //Console.WriteLine("PARSED DOC: ");
                        //Console.WriteLine(Encoding.UTF8.GetString(pdoc));
                        if (_filledSz - i - 1 > 0)
                        {
                            Buffer.BlockCopy(_incomingBuf, i+1, _incomingBuf, 0, _filledSz - i - 1);
                            //Console.WriteLine("INCOMMING BUFFER AFTER PDOC CUT: ");
                            //Console.WriteLine(Encoding.UTF8.GetString(_incommingBuf));
                        }
                            
                        _filledSz -= i + 1;
                        i = -1;
                        inString = false;
                        inEsc = false;
                        braceNestingLevel = 0;
                        JsonDocument? jdoc = null;
                        try
                        {
                            jdoc = JsonDocument.Parse(pdoc);
                        }
                        catch (System.Text.Json.JsonException jex)
                        {
                            Console.WriteLine(jex.Message);
                        }
                        if (jdoc == null)
                        {
                            Console.WriteLine("Malformed request when jdoc not parsed");
                            ParseError(pdoc);
                        }
                        else
                        {
                            JsonElement jobj = jdoc.RootElement;
                            if (!jobj.TryGetProperty("id", out var idVal) || !jobj.TryGetProperty("type", out var typeVal))
                                continue;
                            if (!idVal.TryGetInt32(out var id))
                                id = -1;
                            if (id >= 0)
                            {
                                var mtype = typeVal.ToString();
                                switch (mtype)
                                {
                                    case MsgTypeEnd:
                                    {
                                        _peerEnded = true;
                                        if (_weEnded)
                                        {
                                            Finish();
                                        }
                                        else
                                            EndArrived();
                                        return;
                                    }
                                    case MsgTypeVersion:
                                    {
                                        var ver = 0;
                                        if (jobj.TryGetProperty("version", out var verVal))
                                        {
                                            if (!verVal.TryGetInt32(out ver))
                                                ver = 0;
                                        }
                                        VerArrived(ver);
                                        return;
                                    }
                                    case MsgTypeReq:
                                    case MsgTypeResp:
                                    {
                                        jobj.TryGetProperty("data", out var data);
                                        NewMsgArrived(id, mtype, data);
                                        return;
                                    }
                                    default:
                                        Console.WriteLine("Unknown message type {0}", mtype);
                                        return;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                if (currCh == '\\' && !inEsc)
                    inEsc = true;
                else
                    inEsc = false;
            }
        }
    }

    private static void ParseError(byte[] trash)
    {
        Console.WriteLine("Can't parse:");
        Console.WriteLine(Encoding.UTF8.GetString(trash));
    }

    public void SendReq(JsonReqMessage req)
    {
        if (_weEnded) return;

        string reqJson = JsonConvert.SerializeObject(req);
        Console.WriteLine($"REQ: {reqJson}");
        _sock?.Send(Encoding.UTF8.GetBytes(reqJson));
    }

    public void SendResp(int id, JsonReqData data)
    {
        if (_weEnded) return;

        var resp = new JsonReqMessage
        {
            id = id,
            type = "ans",
            data = data
        };

        var respJson = JsonConvert.SerializeObject(resp);
        _sock?.Send(Encoding.UTF8.GetBytes(respJson));
    }

    public void SendVer(string ver)
    {
        if (_weEnded) return;

        var req = new JsonVersionRequest()
        {
            id = 0,
            type = "ver",
            version = ver
        };

        var reqJson = JsonConvert.SerializeObject(req);
        _sock?.Send(Encoding.UTF8.GetBytes(reqJson));
    }

    public void Finish(bool force = false)
    {
        if (_weEnded && ! force)
        {
            return;
        }
        JsonCommandRequest req = new JsonCommandRequest
        {
            id = 0,
            type = "end"
        };

        var reqJson = JsonConvert.SerializeObject(req);
        _sock?.Send(Encoding.UTF8.GetBytes(reqJson));

        _weEnded = true;

        if (_peerEnded || force) {
            _sock?.Shutdown(SocketShutdown.Both);
            _sock?.Close();
        }
    }

    private void NewMsgArrived(int id, string type, JsonElement data)
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
                ReqArrived?.Invoke(jReq);
                break;
            case MsgTypeResp:
                RespArrived?.Invoke(jReq);
                _responseReceived.Set();
                break;
            default:
                Console.WriteLine("Unsupported message type");
                break;
        }
    }

    private void EndArrived()
    {
        Console.WriteLine("end request received");
        ConnectionClose?.Invoke();
    }

    private void VerArrived(int ver)
    {
        Console.WriteLine($"Version of protocol: {ver}");
    }
    
    #region Delegates and events

    /// <summary> Обработчик события прихода нового запроса </summary>
    /// <param name="reqMessage"> Сообщение-запрос </param>
    public delegate void ReqArrivedEventHandler(JsonMessage reqMessage);

    /// <summary> Обработчик события прихода нового ответа на запрос </summary>
    /// <param name="respMessage"> Сообщение </param>
    public delegate void RespArrivedEventHandler(JsonMessage respMessage);
    
    /// <summary> Обработчик события разрыва подключения </summary>
    public delegate void ConnectionCloseEventHandler();

    /// <summary> Событие прихода нового сообщения </summary>
    public event ReqArrivedEventHandler? ReqArrived;
    
    /// <summary> Событие прихода нового сообщения </summary>
    public event RespArrivedEventHandler? RespArrived;
    
    /// <summary> Событие прихода нового сообщения </summary>
    public event ConnectionCloseEventHandler? ConnectionClose;

    #endregion
}

public class StateObject
{
    // Client socket.
    public Socket? WorkSocket { get; set; }
    public const int BufferSize = 256;
    public JsonProtocolHandler? ProtocolHandler;
    public byte[] Buffer = new byte[BufferSize];
}