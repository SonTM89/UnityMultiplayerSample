using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NetworkMessages
{
    public enum Commands{
        PLAYER_UPDATE,
        SERVER_UPDATE,
        HANDSHAKE,
        PLAYER_INPUT,
        PLAYER_DELETE
    }

    [System.Serializable]
    public class NetworkHeader{
        public Commands cmd;
    }

    [System.Serializable]
    public class HandshakeMsg:NetworkHeader{
        public NetworkObjects.NetworkPlayer player;
        public HandshakeMsg(){      // Constructor
            cmd = Commands.HANDSHAKE;
            player = new NetworkObjects.NetworkPlayer();
        }
        public HandshakeMsg(string _id, Color _cubeColor, Vector3 _cubPos)
        {      // Constructor
            cmd = Commands.HANDSHAKE;
            player = new NetworkObjects.NetworkPlayer();
            player.id = _id;
            player.cubeColor = _cubeColor;
            player.cubPos = _cubPos;
        }
    }
    
    [System.Serializable]
    public class PlayerUpdateMsg:NetworkHeader{
        public NetworkObjects.NetworkPlayer player;
        public PlayerUpdateMsg(){      // Constructor
            cmd = Commands.PLAYER_UPDATE;
            player = new NetworkObjects.NetworkPlayer();
        }

        public PlayerUpdateMsg(string _id, Color _cubeColor, Vector3 _cubPos)
        {      // Constructor
            cmd = Commands.PLAYER_UPDATE;
            player = new NetworkObjects.NetworkPlayer();
            player.id = _id;
            player.cubeColor = _cubeColor;
            player.cubPos = _cubPos;
        }
    };

    // Player Delete Message
    [System.Serializable]
    public class PlayerDeleteMsg : NetworkHeader
    {
        public NetworkObjects.NetworkPlayer player;
        public PlayerDeleteMsg()
        {      // Constructor
            cmd = Commands.PLAYER_DELETE;
            player = new NetworkObjects.NetworkPlayer();
        }

        public PlayerDeleteMsg(string _id, Color _cubeColor, Vector3 _cubPos)
        {      // Constructor
            cmd = Commands.PLAYER_DELETE;
            player = new NetworkObjects.NetworkPlayer();
            player.id = _id;
            player.cubeColor = _cubeColor;
            player.cubPos = _cubPos;
        }
    };

    public class PlayerInputMsg:NetworkHeader{
        public Input myInput;
        public PlayerInputMsg(){
            cmd = Commands.PLAYER_INPUT;
            myInput = new Input();
        }
    }
    [System.Serializable]
    public class  ServerUpdateMsg:NetworkHeader{
        public List<NetworkObjects.NetworkPlayer> players;
        public ServerUpdateMsg(){      // Constructor
            cmd = Commands.SERVER_UPDATE;
            players = new List<NetworkObjects.NetworkPlayer>();
        }
    }
} 

namespace NetworkObjects
{
    [System.Serializable]
    public class NetworkObject{
        public string id;
    }
    [System.Serializable]
    public class NetworkPlayer : NetworkObject{
        public Color cubeColor;
        public Vector3 cubPos;

        public NetworkPlayer(){
            cubeColor = new Color();
            cubPos = new Vector3(0, 0, 0);
        }
    }
}
