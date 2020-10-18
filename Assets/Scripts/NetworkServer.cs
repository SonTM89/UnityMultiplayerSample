using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using System;
using System.Text;
using System.Collections;
using NetworkObjects;
using System.Collections.Generic;

public class NetworkServer : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public ushort serverPort;
    private NativeList<NetworkConnection> m_Connections;

    public List<NetworkObjects.NetworkPlayer> clients;  // List of all connected clients
    private int idGenerator = 0;                        // Client's ID generating
    private const float c_unit = 0.001f;                // constant value used for setting cube color

    void Start ()
    {
        m_Driver = NetworkDriver.Create(new NetworkConfigParameter { disconnectTimeoutMS = 2000 }); // Change disconnect timeout to 2s
        var endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = serverPort;
        if (m_Driver.Bind(endpoint) != 0)
            Debug.Log("Failed to bind to port " + serverPort);
        else
            m_Driver.Listen();

        m_Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);

        
    }

    void SendToClient(string message, NetworkConnection c){
        var writer = m_Driver.BeginSend(NetworkPipeline.Null, c);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }
    public void OnDestroy()
    {
        m_Driver.Dispose();
        m_Connections.Dispose();
    }

    // Add new client to Connected client List
    void _AddNewClient(NetworkConnection c)
    {
        // Randomize cube color
        var rnd = new System.Random();
        Color _c = new Color(c_unit * rnd.Next(0, 1000), c_unit * rnd.Next(0, 1000), c_unit * rnd.Next(0, 1000));
        
        // Set cube position
        float pUnit = clients.Count * 0.25f;
        Vector3 _pos = new Vector3(-0.25f + pUnit, -0.25f + pUnit, -0.25f + pUnit);
        
        clients.Add(new NetworkObjects.NetworkPlayer { id = idGenerator.ToString(), cubeColor = _c, cubPos = _pos });

        // Send info message to new client
        HandshakeMsg m = new HandshakeMsg(idGenerator.ToString(), _c, _pos);
        // Increase ID Generator
        idGenerator++;
        SendToClient(JsonUtility.ToJson(m), c);       
    }

    // Send update message containing all positions of currently connected clients
    IEnumerator _SendUpdate()
    {
        while (true)
        {
            for (int i = 0; i < m_Connections.Length; i++)
            {
                if (!m_Connections[i].IsCreated)
                    continue;
                ServerUpdateMsg msg = new ServerUpdateMsg();
                for (int k = 0; k < clients.Count; k++)
                {
                    msg.players.Add(new NetworkObjects.NetworkPlayer { id = clients[k].id, cubeColor = clients[k].cubeColor, cubPos = clients[k].cubPos });
                }
                SendToClient(JsonUtility.ToJson(msg), m_Connections[i]);
                Debug.Log("Server update!");
            }
            yield return new WaitForSeconds(1/10);
        }
    }

    void OnConnect(NetworkConnection c){
        m_Connections.Add(c);
        Debug.Log("Accepted a connection");

        //// Example to send a handshake message:
        // HandshakeMsg m = new HandshakeMsg();
        // m.player.id = c.InternalId.ToString();
        // SendToClient(JsonUtility.ToJson(m),c);        
        
        _AddNewClient(c);

        // Send update message periodically
        StartCoroutine(_SendUpdate());
    }

    void OnData(DataStreamReader stream, int i){
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length,Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch(header.cmd){
            case Commands.HANDSHAKE:
            HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
            Debug.Log("Handshake message received!");
            break;
            case Commands.PLAYER_UPDATE:
                // Update current position of every connected client
                PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
                for(int j = 0; j < clients.Count; j ++)
                {
                    if(puMsg.player.id == clients[j].id)
                    {
                        clients[j].cubPos = puMsg.player.cubPos;
                    }
                }
                Debug.Log("Player update message received: id(" + puMsg.player.id + "), pos: (" + puMsg.player.cubPos.x + ", " + puMsg.player.cubPos.y + ", " + puMsg.player.cubPos.z + ")");
                break;
            //case Commands.SERVER_UPDATE:
            //ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
            //Debug.Log("Server update message received!");
            //break;
            default:
            Debug.Log("SERVER ERROR: Unrecognized message received!");
            break;
        }
    }

    void OnDisconnect(int i){
        // Send message to inform all connected clients about disconnected client
        // Remove disconnected client from List of all clients
        for (int j = 0; j < clients.Count; j++)
        {
            if(j == i)
            {
                for (int k = 0; k < m_Connections.Length; k++)
                {
                    if (!m_Connections[k].IsCreated)
                        continue;
                    if(k != i)
                    {
                        PlayerDeleteMsg m = new PlayerDeleteMsg(clients[j].id, clients[j].cubeColor, clients[j].cubPos);
                        SendToClient(JsonUtility.ToJson(m), m_Connections[k]);
                        Debug.Log("Client " + clients[j].id + " disconnected from server");
                    }
                }               
                clients.RemoveAtSwapBack(j);
            }
        }
        
        m_Connections[i] = default(NetworkConnection);
    }

    void Update ()
    {
        m_Driver.ScheduleUpdate().Complete();

        // CleanUpConnections
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
            {

                m_Connections.RemoveAtSwapBack(i);
                --i;
            }
        }

        // AcceptNewConnections
        NetworkConnection c = m_Driver.Accept();
        while (c  != default(NetworkConnection))
        {            
            OnConnect(c);

            // Check if there is another new connection
            c = m_Driver.Accept();
        }


        // Read Incoming Messages
        DataStreamReader stream;
        for (int i = 0; i < m_Connections.Length; i++)
        {
            Assert.IsTrue(m_Connections[i].IsCreated);
            
            NetworkEvent.Type cmd;
            cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream);
            while (cmd != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Data)
                {
                    OnData(stream, i);
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    OnDisconnect(i);
                }

                cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream);
            }
        }
    }
}