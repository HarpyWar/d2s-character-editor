﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using CharacterEditor;

namespace IdleClient.Game
{
	partial class GameServer : ClientBase
	{
		List<Item> items = new List<Item>();

		/// <summary>
		/// Handles packets sent from the game server
		/// </summary>
		/// <param name="packet">The packet.</param>
		public void OnPacket(GameServerPacket packet)
		{
			switch (packet.Id)
			{
				case GameServerInPacketType.GameMessage:
					OnGameMessage(packet);
					break;
				case GameServerInPacketType.InformationMessage:
					OnInformationMessage(packet);
					break;
				case GameServerInPacketType.WorldItemAction:
					OnWorldItemAction(packet);
					break;
				case GameServerInPacketType.PlayerInGame:
					OnPlayerInGame(packet);
					break;
				default:
					break;
			}
		}

		/// <summary>
		/// Handles inviting all players already in a game
		/// </summary>
		/// <param name="packet">The packet.</param>
		private void OnPlayerInGame(GameServerPacket packet)
		{
			PlayerInGameIn fromServer = new PlayerInGameIn(packet);

			if (fromServer.IsPlayerInParty)
			{
				return;
			}

			if (String.Compare(characterName, fromServer.PlayerName, System.StringComparison.OrdinalIgnoreCase) != 0)
			{
				// invite player...
				Log(string.Format("Inviting player: {0}", fromServer.PlayerName));
				SendPacket(new PartyRequestOut(PartyRequestOut.RequestType.Invite, fromServer.PlayerID));
			}
		}

		/// <summary>
		/// Handles WorldItemAction (0x9c) packets. Keeps track of items in player's main inventory.
		/// </summary>
		/// <param name="packet">The packet.</param>
		private void OnWorldItemAction(GameServerPacket packet)
		{
			WorldItemEventIn fromServer;

			try
			{
				fromServer = new WorldItemEventIn(packet);
			}
			catch (Exception)
			{
				LogError("Failed to parse item: " + Util.GetPacketDump(packet.Data, true));
			}

			//StringBuilder sb = new StringBuilder();
			//
			//sb.AppendLine("Item: " + fromServer.item);
			//foreach (var item in fromServer.item.Properties)
			//{
			//	sb.AppendLine("  " + item);
			//}
			//sb.AppendLine("  " );
			//LogDebug(sb);

			//if (fromServer.item.Location == Item.ItemLocation.Stored && fromServer.item.StorageId == Item.StorageType.Inventory)
			//{
			//	items.Add(fromServer.item);
			//}
		}

		/// <summary>
		/// Handles InformationMessageIn packets. Keeps track of total number of players
		/// in game.
		/// </summary>
		/// <param name="packet">The packet.</param>
		private void OnInformationMessage(GameServerPacket packet)
		{
			InformationMessageIn fromServer = new InformationMessageIn(packet);
			Log(fromServer);

			switch (fromServer.Event)
			{
				case InformationMessageIn.InformationEvents.PlayerTimeout:
				case InformationMessageIn.InformationEvents.PlayerDropped:
				case InformationMessageIn.InformationEvents.PlayerQuit:
					{
						bool isBot = settings.BotNames.Contains(fromServer.First.ToLower());
						
						PlayerNames.Remove(fromServer.First);
						Log(String.Format("{0}/{1} players remaining", PlayerNames.Count, MaxPlayers));

						FireOnPlayerCountEvent(new PlayerCountArgs(PlayerNames.Count, MaxPlayers, fromServer.First, false, isBot));
						
						break;
					}
				case InformationMessageIn.InformationEvents.PlayerJoined:
					{
						bool isBot = settings.BotNames.Contains(fromServer.First.ToLower());
						
						PlayerNames.Add(fromServer.First);
						Log(String.Format("{0}/{1} players total", PlayerNames.Count, MaxPlayers));

						FireOnPlayerCountEvent(new PlayerCountArgs(PlayerNames.Count, MaxPlayers, fromServer.First, true, isBot));
					}
					break;
				case InformationMessageIn.InformationEvents.PlayerRelation:
					// Accept any party requests
					if (fromServer.Action == 0x02 && fromServer.EntityType == 0x05)
					{
						SendPacket(new PartyRequestOut(PartyRequestOut.RequestType.Accept, fromServer.EntityID));
					}
					break;
				default:
					break;
			}
		}

		public void LeaveGame()
		{
			if (IsInGame)
			{
				IsExiting = true;
				SendPacket(new ExitGameOut());
			}
		}

		/// <summary>
		/// Handles chat messages from other players and objects in game
		/// </summary>
		/// <param name="packet">The packet.</param>
		private void OnGameMessage(GameServerPacket packet)
		{
			GameMessageIn fromServer = new GameMessageIn(packet);
			Log(fromServer);

			if (fromServer.ChatType == GameMessageIn.ChatTypes.ChatMessage)
			{
				if (fromServer.CharacterName.ToLower() == settings.MasterName.ToLower())
				{
					if (fromServer.Message == "#exit")
					{
						Say("Bye");
						FireOnShutdownEvent();
						LeaveGame();
						return;
					}
				}
			}
		}

		/// <summary>
		/// Causes this client to send a chat message to the game
		/// </summary>
		/// <param name="message">The message.</param>
		public void Say(string message)
		{
			SendMessageOut toServer = new SendMessageOut(message);
			SendPacket(toServer);
		}

		/// <summary>
		/// Causes this client to send a chat message to a player.
		/// </summary>
		/// <param name="message">The message.</param>
		/// <param name="target">Player to send message to.</param>
		public void Say(string message, string target)
		{
			SendMessageOut toServer = new SendMessageOut(message, target);
			SendPacket(toServer);
		}

		/// <summary>
		/// Causes this client to send an overhead message.
		/// </summary>
		/// <param name="message">The message.</param>
		public void SayOverhead(string message)
		{
			SendOverheadMessageOut toServer = new SendOverheadMessageOut(message);
			SendPacket(toServer);
		}

		/// <summary>
		/// Pings the game server
		/// </summary>
		public void SendPing()
		{
			PingOut toServer;

			// 0x35 is just some number I threw in there that was close to what the real d2 client
			// was sending, probably works fine with 0.
			toServer = new PingOut((uint)DateTime.Now.Ticks, 0x35);

			SendPacket(toServer);
		}
	}
}
