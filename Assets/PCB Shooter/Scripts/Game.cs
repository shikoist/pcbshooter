using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BeardedManStudios.Forge.Networking;
using BeardedManStudios.Forge.Networking.Generated;

public class Game : GameBehavior
{
    float timerPlayerList;
    float rateTimerPlayerList = 1;

    Text plText;

    protected override void NetworkStart() {
        base.NetworkStart();

        //networkObject.Networker.pingReceived

        if (networkObject.IsOwner) {
            
        }
    }

    // Start is called before the first frame update
    private void Start()
    {
        plText = GameObject.Find("PlayerList").GetComponent<Text>();
    }

    // Update is called once per frame
    private void Update()
    {
        if (timerPlayerList < Time.time) {
            timerPlayerList += rateTimerPlayerList + Time.time;

            if (networkObject.Networker.IsServer) {
                plText.text = "";
                //if (networkObject.Networker != null) {

                //    for (int i = 0; i < networkObject.Networker.Players.Count; i++) {
                //        GameObject playerGO = GameObject.Find(networkObject.Networker.Players[i].Name);
                //        if (playerGO != null) {
                //            Player player = playerGO.GetComponent<Player>();
                //            plText.text += "Player " + i + " " + 
                //                networkObject.Networker.Players[i].Name + " " +
                //                //networkObject.Networker.Players[i].Ip + " " +
                //                //networkObject.Networker.Players[i].LastPing + " " +
                //                player.PlayerPing +
                //                "\n";
                //        }
                //    }
                //}

                GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
                for (int i = 0; i < players.Length; i++) {
                    Player player = players[i].GetComponent<Player>();
                    if (player != null) {
                        plText.text +=
                            player.ID + " " +
                            player.Name + " " +
                            player.Kills + " " +
                            player.PlayerPing + " " +
                            "\n";
                    }
                }

                networkObject.SendRpc(RPC_UPDATE_PLAYERS_LIST, Receivers.AllBuffered, plText.text);
            }
        }
    }
    
    // Override the abstract RPC method that we made in the NCW
    public override void UpdatePlayersList(RpcArgs args) {
        // Since there is only 1 argument and it is a string we can safely
        // cast the first argument to a string knowing that it is going to
        // be the name for this player
        MainScript main = GameObject.FindObjectOfType<MainScript>();

        main.playerListText.text = args.GetNext<string>();
    }
}
