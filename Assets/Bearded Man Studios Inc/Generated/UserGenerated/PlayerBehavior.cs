using BeardedManStudios.Forge.Networking;
using BeardedManStudios.Forge.Networking.Unity;
using UnityEngine;

namespace BeardedManStudios.Forge.Networking.Generated
{
	[GeneratedRPC("{\"types\":[[\"string\"][\"int\"][\"int\"][\"Vector3\", \"Vector3\"][\"int\", \"uint\", \"Vector3\", \"Vector3\"][\"uint\"][\"Vector3\", \"Quaternion\"]]")]
	[GeneratedRPCVariableNames("{\"types\":[[\"newName\"][\"newPing\"][\"newKills\"][\"origin\", \"camForward\"][\"damage\", \"playerID\", \"point\", \"normal\"][\"newId\"][\"position\", \"rotation\"]]")]
	public abstract partial class PlayerBehavior : NetworkBehavior
	{
		public const byte RPC_UPDATE_NAME = 0 + 5;
		public const byte RPC_UPDATE_PING = 1 + 5;
		public const byte RPC_UPDATE_KILLS = 2 + 5;
		public const byte RPC_SHOOT = 3 + 5;
		public const byte RPC_TAKE_DAMAGE = 4 + 5;
		public const byte RPC_UPDATE_ID = 5 + 5;
		public const byte RPC_SPAWN = 6 + 5;
		
		public PlayerNetworkObject networkObject = null;

		public override void Initialize(NetworkObject obj)
		{
			// We have already initialized this object
			if (networkObject != null && networkObject.AttachedBehavior != null)
				return;
			
			networkObject = (PlayerNetworkObject)obj;
			networkObject.AttachedBehavior = this;

			base.SetupHelperRpcs(networkObject);
			networkObject.RegisterRpc("UpdateName", UpdateName, typeof(string));
			networkObject.RegisterRpc("UpdatePing", UpdatePing, typeof(int));
			networkObject.RegisterRpc("UpdateKills", UpdateKills, typeof(int));
			networkObject.RegisterRpc("Shoot", Shoot, typeof(Vector3), typeof(Vector3));
			networkObject.RegisterRpc("TakeDamage", TakeDamage, typeof(int), typeof(uint), typeof(Vector3), typeof(Vector3));
			networkObject.RegisterRpc("UpdateId", UpdateId, typeof(uint));
			networkObject.RegisterRpc("Spawn", Spawn, typeof(Vector3), typeof(Quaternion));

			networkObject.onDestroy += DestroyGameObject;

			if (!obj.IsOwner)
			{
				if (!skipAttachIds.ContainsKey(obj.NetworkId))
					ProcessOthers(gameObject.transform, obj.NetworkId + 1);
				else
					skipAttachIds.Remove(obj.NetworkId);
			}

			if (obj.Metadata != null)
			{
				byte transformFlags = obj.Metadata[0];

				if (transformFlags != 0)
				{
					BMSByte metadataTransform = new BMSByte();
					metadataTransform.Clone(obj.Metadata);
					metadataTransform.MoveStartIndex(1);

					if ((transformFlags & 0x01) != 0 && (transformFlags & 0x02) != 0)
					{
						MainThreadManager.Run(() =>
						{
							transform.position = ObjectMapper.Instance.Map<Vector3>(metadataTransform);
							transform.rotation = ObjectMapper.Instance.Map<Quaternion>(metadataTransform);
						});
					}
					else if ((transformFlags & 0x01) != 0)
					{
						MainThreadManager.Run(() => { transform.position = ObjectMapper.Instance.Map<Vector3>(metadataTransform); });
					}
					else if ((transformFlags & 0x02) != 0)
					{
						MainThreadManager.Run(() => { transform.rotation = ObjectMapper.Instance.Map<Quaternion>(metadataTransform); });
					}
				}
			}

			MainThreadManager.Run(() =>
			{
				NetworkStart();
				networkObject.Networker.FlushCreateActions(networkObject);
			});
		}

		protected override void CompleteRegistration()
		{
			base.CompleteRegistration();
			networkObject.ReleaseCreateBuffer();
		}

		public override void Initialize(NetWorker networker, byte[] metadata = null)
		{
			Initialize(new PlayerNetworkObject(networker, createCode: TempAttachCode, metadata: metadata));
		}

		private void DestroyGameObject(NetWorker sender)
		{
			MainThreadManager.Run(() => { try { Destroy(gameObject); } catch { } });
			networkObject.onDestroy -= DestroyGameObject;
		}

		public override NetworkObject CreateNetworkObject(NetWorker networker, int createCode, byte[] metadata = null)
		{
			return new PlayerNetworkObject(networker, this, createCode, metadata);
		}

		protected override void InitializedTransform()
		{
			networkObject.SnapInterpolations();
		}

		/// <summary>
		/// Arguments:
		/// string newName
		/// </summary>
		public abstract void UpdateName(RpcArgs args);
		/// <summary>
		/// Arguments:
		/// int newPing
		/// </summary>
		public abstract void UpdatePing(RpcArgs args);
		/// <summary>
		/// Arguments:
		/// int newKills
		/// </summary>
		public abstract void UpdateKills(RpcArgs args);
		/// <summary>
		/// Arguments:
		/// Vector3 origin
		/// Vector3 camForward
		/// </summary>
		public abstract void Shoot(RpcArgs args);
		/// <summary>
		/// Arguments:
		/// int damage
		/// uint playerID
		/// Vector3 point
		/// Vector3 normal
		/// </summary>
		public abstract void TakeDamage(RpcArgs args);
		/// <summary>
		/// Arguments:
		/// uint newId
		/// </summary>
		public abstract void UpdateId(RpcArgs args);
		/// <summary>
		/// Arguments:
		/// Vector3 position
		/// Quaternion rotation
		/// </summary>
		public abstract void Spawn(RpcArgs args);

		// DO NOT TOUCH, THIS GETS GENERATED PLEASE EXTEND THIS CLASS IF YOU WISH TO HAVE CUSTOM CODE ADDITIONS
	}
}