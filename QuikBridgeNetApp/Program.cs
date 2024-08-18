// See https://aka.ms/new-console-template for more information

using QuikBridgeNet;

string host = "127.0.0.1";
int port = 57777;
            
QuikBridge bridge = new QuikBridge(host, port);
bridge.GetClassesList();