using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using NetworkObjects;
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;

public class NetworkClient : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public NetworkConnection m_Connection;
    public string serverIP;
    public ushort serverPort;

    private float speed = 10; // Moving speed

    // Current player and cube
    public NetworkObjects.NetworkPlayer currentplayer;
    public GameObject currentCube;

    // List of all connected players and cubes
    public List<NetworkObjects.NetworkPlayer> connectedPlayers;
    public List<GameObject> connectedCubes;
    
    void Start ()
    {
        m_Driver = NetworkDriver.Create();
        m_Connection = default(NetworkConnection);
        var endpoint = NetworkEndPoint.Parse(serverIP,serverPort);
        m_Connection = m_Driver.Connect(endpoint);

        
    }
    
    void SendToServer(string message){
        var writer = m_Driver.BeginSend(m_Connection);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }

    void OnConnect(){
        Debug.Log("We are now connected to the server");

        //// Example to send a handshake message:
        // HandshakeMsg m = new HandshakeMsg();
        // m.player.id = m_Connection.InternalId.ToString();
        // SendToServer(JsonUtility.ToJson(m));

        // Send position update periodically
        StartCoroutine(SendPositionUpdate());
    }

    // Allow player to move cube around using WASD keys
    public void _CubeMove()
    {
        Vector3 velocity = new Vector3(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"), 0) * speed * Time.deltaTime;

        if (currentCube != null)
        {
            currentCube.transform.Translate(velocity);
            currentplayer.cubPos = currentCube.transform.position;
        }
    }

    // Send position updates to the server
    IEnumerator SendPositionUpdate()
    {
        while (true)
        {
            yield return new WaitForSeconds(1/10);
            Debug.Log("Sending Position Update");
            PlayerUpdateMsg msg = new PlayerUpdateMsg(currentplayer.id, currentplayer.cubeColor, currentplayer.cubPos);
            SendToServer(JsonUtility.ToJson(msg));
        }
    }

    void OnData(DataStreamReader stream){
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length,Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch(header.cmd){
            case Commands.HANDSHAKE:
                // Setting current player and cube using info receiving from server
                HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
                
                currentplayer.id = hsMsg.player.id;
                currentplayer.cubPos = hsMsg.player.cubPos;
                currentplayer.cubeColor = hsMsg.player.cubeColor;
                
                currentCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                currentCube.GetComponent<Renderer>().material.color = new Color(currentplayer.cubeColor.r, currentplayer.cubeColor.g, currentplayer.cubeColor.b);
                currentCube.transform.position = currentplayer.cubPos;
                

                //PlayerUpdateMsg msg = new PlayerUpdateMsg(currentplayer.id, currentplayer.cubeColor, currentplayer.cubPos);
                //SendToServer(JsonUtility.ToJson(msg));
                Debug.Log("Handshake message received!" + recMsg);
                break;
            //case Commands.PLAYER_UPDATE:
                //PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
                //Debug.Log("Player update message received!");
                //break;
            case Commands.SERVER_UPDATE:
                // Add all connected player and cube info not exist in connected clients list
                // Update info of existed clients
                ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
                for (int i = 0; i < suMsg.players.Count; i++)
                {
                    bool existed = false;

                    if (suMsg.players[i].id != currentplayer.id)
                    {
                        for (int j = 0; j < connectedPlayers.Count; j++)
                        {
                            if (suMsg.players[i].id == connectedPlayers[j].id)
                            {
                                connectedPlayers[j].cubPos = suMsg.players[i].cubPos;
                                connectedCubes[j].transform.position = suMsg.players[i].cubPos;
                                existed = true;
                                break;
                            }
                        }

                        if(existed == false)
                        {
                            connectedPlayers.Add(new NetworkObjects.NetworkPlayer { id = suMsg.players[i].id, cubeColor = suMsg.players[i].cubeColor, cubPos = suMsg.players[i].cubPos });
                            GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            tempCube.GetComponent<Renderer>().material.color = new Color(suMsg.players[i].cubeColor.r, suMsg.players[i].cubeColor.g, suMsg.players[i].cubeColor.b);
                            tempCube.transform.position = suMsg.players[i].cubPos;
                            connectedCubes.Add(tempCube);
                        }
                    }
                    
                }
                Debug.Log("Server update message received!" + recMsg);
                break;
            case Commands.PLAYER_DELETE:
                // Remove disconnected player from connected clients list and destroy this player's cube
                PlayerDeleteMsg pdMsg = JsonUtility.FromJson<PlayerDeleteMsg>(recMsg);
                for (int j = 0; j < connectedPlayers.Count; j++)
                {
                    if(connectedPlayers[j].id == pdMsg.player.id)
                    {
                        connectedPlayers.RemoveAtSwapBack(j);
                        connectedCubes[j].SetActive(false);
                        connectedCubes.RemoveAtSwapBack(j);
                    }
                }
                Debug.Log("Client " + pdMsg.player.id + " disconnected from server");
                break;
            default:
            Debug.Log("Unrecognized message received!");
            break;
        }
    }

    void Disconnect(){
        m_Connection.Disconnect(m_Driver);
        m_Connection = default(NetworkConnection);
    }

    void OnDisconnect(){
        Debug.Log("Client got disconnected from server");
        m_Connection = default(NetworkConnection);
    }

    public void OnDestroy()
    {
        m_Driver.Dispose();
    }   
    void Update()
    {
        _CubeMove();

        m_Driver.ScheduleUpdate().Complete();

        if (!m_Connection.IsCreated)
        {
            return;
        }

        DataStreamReader stream;
        NetworkEvent.Type cmd;
        cmd = m_Connection.PopEvent(m_Driver, out stream);
        while (cmd != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                OnConnect();
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                OnData(stream);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                OnDisconnect();
            }

            cmd = m_Connection.PopEvent(m_Driver, out stream);
        }
    }
}