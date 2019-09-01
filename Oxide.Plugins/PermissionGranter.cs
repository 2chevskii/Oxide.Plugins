using System.Collections.Generic;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
	[Info("Permission Granter", "2CHEVSKII", "0.1.0")]
	class PermissionGranter:CovalencePlugin
	{
		[Command("pgrant")]
		void CmdPgrant(IPlayer player, string command, string[] args)
		{
			if(!player.IsAdmin && !player.IsServer)
			{
				player.Message(GetLocalizedMessage(player, "no perm"));
			}
			else if(args.Length < 1)
			{
				player.Message(GetLocalizedMessage(player, "usage general"));
			}
			else
			{
				string name;
				string permissionName;
				switch(args[0].ToLower())
				{
					case "grant":
						if(args.Length < 4)
						{
							player.Message(GetLocalizedMessage(player, "usage grant-revoke"));
						}
						else
						{
							switch(args[1].ToLower())
							{
								case "group":
									name = args[2];
									if(!permission.GroupExists(name))
									{
										player.Message(GetLocalizedMessage(player, "group not found", name));
									}
									else
									{
										permissionName = args[3].ToLower();
										if(!permission.PermissionExists(permissionName))
										{
											player.Message(GetLocalizedMessage(player, "perm not found", permissionName));
										}
										else
										{
											permission.GrantGroupPermission(name, permissionName, GetPermissionPlugin(permissionName));
											player.Message(GetLocalizedMessage(player, "granted", permissionName, name));
										}
									}
									break;

								case "user":
									name = args[2];
									if(!permission.UserExists(name))
									{
										player.Message(GetLocalizedMessage(player, "user not found", name));
									}
									else
									{
										permissionName = args[3].ToLower();
										if(!permission.PermissionExists(permissionName))
										{
											player.Message(GetLocalizedMessage(player, "perm not found", permissionName));
										}
										else
										{
											permission.GrantUserPermission(name, permissionName, GetPermissionPlugin(permissionName));
											player.Message(GetLocalizedMessage(player, "granted", permissionName, name));
										}
									}
									break;

								default:
									player.Message(GetLocalizedMessage(player, "usage grant-revoke"));
									break;

							}
						}
						break;
					case "revoke":
						if(args.Length < 4)
						{
							player.Message(GetLocalizedMessage(player, "usage grant-revoke"));
						}
						else
						{
							switch(args[1].ToLower())
							{
								case "group":
									name = args[2];
									if(!permission.GroupExists(name))
									{
										player.Message(GetLocalizedMessage(player, "group not found", name));
									}
									else
									{
										permissionName = args[3].ToLower();
										if(!permission.PermissionExists(permissionName))
										{
											player.Message(GetLocalizedMessage(player, "perm not found", permissionName));
										}
										else if(!permission.GroupHasPermission(name, permissionName))
										{
											player.Message(GetLocalizedMessage(player, "group no perm", name, permissionName));
										}
										else
										{
											permission.RevokeGroupPermission(name, permissionName);
											player.Message(GetLocalizedMessage(player, "revoked", permissionName, name));
										}
									}

									break;

								case "user":
									name = args[2];
									if(!permission.UserExists(name))
									{
										player.Message(GetLocalizedMessage(player, "user not found", name));
									}
									else
									{
										permissionName = args[3].ToLower();
										if(!permission.PermissionExists(permissionName))
										{
											player.Message(GetLocalizedMessage(player, "perm not found", permissionName));
										}
										else if(!permission.UserHasPermission(name, permissionName))
										{
											player.Message(GetLocalizedMessage(player, "user no perm", name, permissionName));
										}
										else
										{
											permission.RevokeUserPermission(name, permissionName);
											player.Message(GetLocalizedMessage(player, "revoked", permissionName, name));
										}
									}

									break;

								default:
									player.Message(GetLocalizedMessage(player, "usage grant-revoke"));
									break;

							}
						}
						break;

					case "show":
						if(args.Length < 2)
						{
							player.Message(GetLocalizedMessage(player, "usage show"));
						}
						else
						{
							switch(args[1].ToLower())
							{
								case "perms":
									player.Message(string.Join(", ", permission.GetPermissions()));
									break;
								case "group":
									if(args.Length < 3)
									{
										player.Message(GetLocalizedMessage(player, "usage show"));
									}
									else
									{
										name = args[2];
										if(!permission.GroupExists(name))
										{
											player.Message(GetLocalizedMessage(player, "group not found", name));
										}
										else
										{
											var perms = permission.GetGroupPermissions(name);
											player.Message(perms.Length < 1 ? GetLocalizedMessage(player, "zero perms") : string.Join(", ", perms));
										}
									}
									break;
								case "user":
									if(args.Length < 3)
									{
										player.Message(GetLocalizedMessage(player, "usage show"));
									}
									else
									{
										name = args[2];
										if(!permission.UserExists(name))
										{
											player.Message(GetLocalizedMessage(player, "user not found", name));
										}
										else
										{
											var perms = permission.GetUserPermissions(name);
											player.Message(perms.Length < 1 ? GetLocalizedMessage(player, "zero perms") : string.Join(", ", perms));
										}
									}
									break;
								case "groups":
									player.Message(string.Join(", ", permission.GetGroups()));
									break;
								default:
									player.Message(GetLocalizedMessage(player, "usage show"));
									break;
							}
						}
						break;

					default:
						player.Message(GetLocalizedMessage(player, "usage general"));
						break;
				}
			}
		}

		private Dictionary<string, string> defaultmessages_en = new Dictionary<string, string>
		{
			["usage grant-revoke"] = "Usage: pgrant <grant/revoke> <group/user> <groupname/username> <permission>",
			["usage show"] = "Usage: pgrant show <perms/group/user/groups> <.../groupname/username/...>",
			["user not found"] = "User {0} does not exists",
			["group not found"] = "Group {0} does not exists",
			["perm not found"] = "Permission {0} does not exists",
			["user no perm"] = "User {0} does not have permission {1}",
			["group no perm"] = "Group {0} does not have permission {1}",
			["no perm"] = "You are not allowed to use this command",
			["usage general"] = "Usage: pgrant <grant/revoke/show>",
			["zero perms"] = "This group or user has no permissions granted",
			["granted"] = "Granted permission {0} to group/user {1}",
			["revoked"] = "Revoked permission {0} from group/user {1}"
		};

		protected override void LoadDefaultMessages() => lang.RegisterMessages(defaultmessages_en, this);

		string GetLocalizedMessage(IPlayer player, string key,params object[] args) => string.Format(lang.GetMessage(key, this, player.Id), args);

		Plugin GetPermissionPlugin(string perm)
		{
			var permset = typeof(Permission).GetField("permset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(permission) as Dictionary<Plugin, HashSet<string>>;
			if(permset == null)
			{
				return null;
			}

			foreach(var entry in permset)
			{
				if(entry.Value.Contains(perm))
				{
					return entry.Key;
				}
			}

			return null;
		}

	}
}
